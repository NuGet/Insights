using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.Delta.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Entities
{
    public class V2ToDatabaseProcessor
    {
        public static readonly TimeSpan FuzzFactor = TimeSpan.FromHours(1);

        private readonly CursorService _cursorService;
        private readonly V2Client _v2Client;
        private readonly PackageService _service;
        private readonly IBatchSizeProvider _batchSizeProvider;
        private readonly IOptions<ExplorePackagesEntitiesSettings> _options;
        private readonly ILogger<V2ToDatabaseProcessor> _logger;

        public V2ToDatabaseProcessor(
            CursorService cursorService,
            V2Client v2Client,
            PackageService service,
            IBatchSizeProvider batchSizeProvider,
            IOptions<ExplorePackagesEntitiesSettings> options,
            ILogger<V2ToDatabaseProcessor> logger)
        {
            _cursorService = cursorService;
            _v2Client = v2Client;
            _service = service;
            _batchSizeProvider = batchSizeProvider;
            _options = options;
            _logger = logger;
        }

        public async Task UpdateAsync(IReadOnlyList<PackageIdentity> identities)
        {
            var packagesOrNull = await TaskProcessor.ExecuteAsync(
                identities,
                async identity =>
                {
                    var package = await _v2Client.GetPackageOrNullAsync(
                        _options.Value.V2BaseUrl,
                        identity.Id,
                        identity.Version);

                    if (package == null)
                    {
                        _logger.LogWarning("Package {Id} {Version} does not exist on V2.", identity.Id, identity.Version);
                    }

                    return package;
                },
                workerCount: _options.Value.WorkerCount,
                token: CancellationToken.None);
            var packages = packagesOrNull.Where(x => x != null).ToList();
            await _service.AddOrUpdatePackagesAsync(packages);
        }

        public async Task UpdateAsync(V2OrderByTimestamp orderBy)
        {
            switch (orderBy)
            {
                case V2OrderByTimestamp.Created:
                    await UpdateAsync(
                        CursorNames.V2ToDatabaseCreated,
                        V2OrderByTimestamp.Created,
                        x => x.Created);
                    break;
                case V2OrderByTimestamp.LastEdited:
                    await UpdateAsync(
                        CursorNames.V2ToDatabaseLastEdited,
                        V2OrderByTimestamp.LastEdited,
                        x => x.LastEdited ?? DateTimeOffset.MinValue);
                    break;
                default:
                    throw new NotSupportedException($"V2 order by mode {orderBy} is not supported.");
            }
        }

        private async Task UpdateAsync(
            string cursorName,
            V2OrderByTimestamp orderBy,
            Func<V2Package, DateTimeOffset> getTimestamp)
        {
            var taskQueue = new TaskQueue<IReadOnlyList<V2Package>>(
                workerCount: 1,
                maxQueueSize: 50,
                produceAsync: (ctx, t) => ProduceAsync(ctx, cursorName, orderBy, getTimestamp, t),
                consumeAsync: (x, t) => ConsumeAsync(x, cursorName, getTimestamp),
                logger: _logger);

            await taskQueue.RunAsync();
        }

        private async Task ProduceAsync(
            IProducerContext<IReadOnlyList<V2Package>> taskQueue,
            string cursorName,
            V2OrderByTimestamp orderBy,
            Func<V2Package, DateTimeOffset> getTimestamp,
            CancellationToken token)
        {
            var start = await _cursorService.GetValueAsync(cursorName);
            if (start > DateTimeOffset.MinValue.Add(FuzzFactor))
            {
                start = start.Subtract(FuzzFactor);
            }

            var complete = false;
            do
            {
                token.ThrowIfCancellationRequested();

                var batchSize = _batchSizeProvider.Get(BatchSizeType.V2ToDatabase);

                var packages = await _v2Client.GetPackagesAsync(
                   _options.Value.V2BaseUrl,
                   orderBy,
                   start,
                   batchSize);

                // If we have a full page, take only packages with a created timestamp less than the max.
                if (packages.Count == batchSize)
                {
                    var max = packages.Max(getTimestamp);
                    var packagesBeforeMax = packages
                        .Where(x => getTimestamp(x) < max)
                        .ToList();

                    if (packages.Any()
                        && !packagesBeforeMax.Any())
                    {
                        throw new InvalidOperationException($"All of the packages in the page have the same {orderBy} timestamp.");
                    }

                    packages = packagesBeforeMax;
                }
                else
                {
                    complete = true;
                }

                if (packages.Count > 0)
                {
                    await taskQueue.EnqueueAsync(packages, token);
                    start = packages.Max(getTimestamp);
                }
            }
            while (!complete);
        }

        private async Task ConsumeAsync(
            IReadOnlyList<V2Package> packages,
            string cursorName,
            Func<V2Package, DateTimeOffset> getTimestamp)
        {
            var oldCursor = await _cursorService.GetValueAsync(cursorName);

            await _service.AddOrUpdatePackagesAsync(packages);

            var newCursor = packages.Max(getTimestamp);
            if (newCursor > oldCursor)
            {
                await _cursorService.SetValueAsync(cursorName, newCursor);
                _logger.LogInformation("[CHECKPOINT] Cursor {CursorName} moved to {Start:O}.", cursorName, newCursor);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class V2ToDatabaseProcessor
    {
        private static readonly TimeSpan FuzzFactor = TimeSpan.FromHours(1);
        private const int PageSize = 100;
        private readonly CursorService _cursorService;
        private readonly V2Client _v2Client;
        private readonly PackageService _service;
        private readonly ExplorePackagesSettings _settings;

        public V2ToDatabaseProcessor(
            CursorService cursorService,
            V2Client v2Client,
            PackageService service,
            ExplorePackagesSettings settings)
        {
            _cursorService = cursorService;
            _v2Client = v2Client;
            _service = service;
            _settings = settings;
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
                workAsync: x => ConsumeAsync(x, cursorName, getTimestamp));

            taskQueue.Start();

            await ProduceAsync(taskQueue, cursorName, orderBy, getTimestamp);

            await taskQueue.CompleteAsync();
        }

        private async Task ProduceAsync(
            TaskQueue<IReadOnlyList<V2Package>> taskQueue,
            string cursorName,
            V2OrderByTimestamp orderBy,
            Func<V2Package, DateTimeOffset> getTimestamp)
        {
            var start = await _cursorService.GetValueAsync(cursorName);
            if (start > DateTimeOffset.MinValue.Add(FuzzFactor))
            {
                start = start.Subtract(FuzzFactor);
            }

            var complete = false;
            do
            {
                var packages = await _v2Client.GetPackagesAsync(
                   _settings.V2BaseUrl,
                   orderBy,
                   start,
                   PageSize);

                // If we have a full page, take only packages with a created timestamp less than the max.
                if (packages.Count == PageSize)
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

                while (taskQueue.Count > 50)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
                
                if (packages.Count > 0)
                {
                    taskQueue.Enqueue(packages);
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
            var oldCUrsor = await _cursorService.GetValueAsync(cursorName);

            await _service.AddOrUpdatePackagesAsync(packages);

            var newCursor = packages.Max(getTimestamp);
            if (newCursor > oldCUrsor)
            {
                await _cursorService.SetValueAsync(cursorName, newCursor);
            }
        }
    }
}

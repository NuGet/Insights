using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic
{
    public class DependencyPackagesToDatabaseCommitProcessor : ICommitProcessor<PackageRegistrationEntity, PackageDependencyEntity, long?>
    {
        private readonly PackageDependencyService _packageDependencyService;
        private readonly IBatchSizeProvider _batchSizeProvider;
        private readonly ILogger<DependencyPackagesToDatabaseCommitProcessor> _logger;

        public DependencyPackagesToDatabaseCommitProcessor(
            PackageDependencyService packageDependencyService,
            IBatchSizeProvider batchSizeProvider,
            ILogger<DependencyPackagesToDatabaseCommitProcessor> logger)
        {
            _packageDependencyService = packageDependencyService;
            _batchSizeProvider = batchSizeProvider;
            _logger = logger;
        }

        public string CursorName => CursorNames.DependencyPackagesToDatabase;

        public IReadOnlyList<string> DependencyCursorNames => new[]
        {
            CursorNames.DependenciesToDatabase,
        };

        public int BatchSize => _batchSizeProvider.Get(BatchSizeType.DependencyPackagesToDatabase_PackageRegistrations);

        public string SerializeProgressToken(long? progressToken)
        {
            if (!progressToken.HasValue)
            {
                return null;
            }

            return progressToken.Value.ToString(CultureInfo.InvariantCulture);
        }

        public long? DeserializeProgressToken(string serializedProgressToken)
        {
            if (serializedProgressToken == null)
            {
                return null;
            }

            return long.Parse(serializedProgressToken, CultureInfo.InvariantCulture);
        }

        public async Task<ItemBatch<PackageDependencyEntity, long?>> InitializeItemsAsync(
            IReadOnlyList<PackageRegistrationEntity> entities,
            long? progressToken,
            CancellationToken token)
        {
            var packageRegistrationKeyToId = entities
                .ToDictionary(x => x.PackageRegistrationKey, x => x.Id);

            var packageRegistrationKeys = packageRegistrationKeyToId.Keys.ToList();

            var packagesBatchSize = _batchSizeProvider.Get(BatchSizeType.DependencyPackagesToDatabase_Packages);

            var dependents = await _packageDependencyService.GetDependentPackagesAsync(
                packageRegistrationKeys,
                progressToken,
                take: packagesBatchSize);

            var topDependencyPairs = dependents
                .GroupBy(x => x.DependencyPackageRegistrationKey)
                .ToDictionary(
                    x => packageRegistrationKeyToId[x.Key],
                    x => x.Select(y => y.ParentPackageKey).Distinct().Count())
                .OrderByDescending(x => x.Value)
                .Take(5)
                .ToList();

            if (topDependencyPairs.Any())
            {
                var width = topDependencyPairs.Max(x => x.Value.ToString().Length);

                _logger.LogInformation(
                    $"Top dependencies:{Environment.NewLine}" +
                    string.Join(
                        Environment.NewLine,
                        topDependencyPairs.Select((x, i) => $"  {x.Value.ToString().PadLeft(width)} {x.Key}")));
            }

            long? nextAfterKey = null;
            if (dependents.Any())
            {
                nextAfterKey = dependents.Max(x => x.PackageDependencyKey);
            }

            return new ItemBatch<PackageDependencyEntity, long?>(dependents, nextAfterKey.HasValue, nextAfterKey);
        }

        public async Task ProcessBatchAsync(IReadOnlyList<PackageDependencyEntity> batch)
        {
            await _packageDependencyService.UpdateDependencyPackagesAsync(batch);
        }
    }
}

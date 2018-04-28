using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Logic
{
    public class DependencyPackagesToDatabaseCommitProcessor : ICommitProcessor<PackageRegistrationEntity, PackageDependencyEntity>
    {
        private const int ItemBatchSize = 10000;
        private readonly PackageDependencyService _packageDependencyService;
        private readonly ILogger _log;

        public DependencyPackagesToDatabaseCommitProcessor(
            PackageDependencyService packageDependencyService,
            ILogger log)
        {
            _packageDependencyService = packageDependencyService;
            _log = log;
        }

        public string CursorName => CursorNames.DependencyPackagesToDatabase;

        public IReadOnlyList<string> DependencyCursorNames => new[]
        {
            CursorNames.DependenciesToDatabase,
        };

        public int BatchSize => 100;

        public async Task<ItemBatch<PackageDependencyEntity>> InitializeItemsAsync(
            IReadOnlyList<PackageRegistrationEntity> entities,
            int skip,
            CancellationToken token)
        {
            var packageRegistrationKeyToId = entities
                .ToDictionary(x => x.PackageRegistrationKey, x => x.Id);

            var packageRegistrationKeys = packageRegistrationKeyToId.Keys.ToList();

            var dependents = await _packageDependencyService.GetDependentPackagesAsync(
                packageRegistrationKeys,
                skip,
                take: ItemBatchSize);

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

                _log.LogInformation(
                    $"Top dependencies:{Environment.NewLine}" +
                    string.Join(
                        Environment.NewLine,
                        topDependencyPairs.Select((x, i) => $"  {x.Value.ToString().PadLeft(width)} {x.Key}")));
            }

            return new ItemBatch<PackageDependencyEntity>(dependents, dependents.Count >= ItemBatchSize);
        }

        public async Task ProcessBatchAsync(IReadOnlyList<PackageDependencyEntity> batch)
        {
            await _packageDependencyService.UpdateDependencyPackagesAsync(batch);
        }
    }
}

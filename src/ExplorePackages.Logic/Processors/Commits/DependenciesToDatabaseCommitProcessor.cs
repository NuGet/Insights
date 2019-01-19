using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;

namespace Knapcode.ExplorePackages.Logic
{
    public class DependenciesToDatabaseCommitProcessor : ICommitProcessor<PackageEntity, PackageDependencyGroups>
    {
        private readonly NuspecStore _nuspecStore;
        private readonly PackageDependencyService _packageDependencyService;

        public DependenciesToDatabaseCommitProcessor(
            NuspecStore nuspecStore,
            PackageDependencyService packageDependencyService)
        {
            _nuspecStore = nuspecStore;
            _packageDependencyService = packageDependencyService;
        }

        public string CursorName => CursorNames.DependenciesToDatabase;

        public IReadOnlyList<string> DependencyCursorNames { get; } = new[]
        {
            CursorNames.CatalogToNuspecs,
            CursorNames.CatalogToDatabase,
        };

        public int BatchSize => 1000;

        public async Task<ItemBatch<PackageDependencyGroups>> InitializeItemsAsync(
            IReadOnlyList<PackageEntity> packages,
            int skip,
            CancellationToken token)
        {
            var output = new List<PackageDependencyGroups>();

            foreach (var package in packages)
            {
                await InitializeItemAsync(output, package);
            }

            return new ItemBatch<PackageDependencyGroups>(output, hasMoreItems: false);
        }

        private async Task InitializeItemAsync(List<PackageDependencyGroups> output, PackageEntity package)
        {
            var nuspec = await _nuspecStore.GetNuspecContextAsync(package.PackageRegistration.Id, package.Version);
            if (nuspec.Document == null)
            {
                return;
            }

            var identity = new PackageIdentity(package.PackageRegistration.Id, package.Version);
            var dependencyGroups = NuspecUtility.GetParsedDependencyGroups(nuspec.Document);
            var packageDependencyGroups = new PackageDependencyGroups(identity, dependencyGroups);

            output.Add(packageDependencyGroups);
        }

        public async Task ProcessBatchAsync(IReadOnlyList<PackageDependencyGroups> batch)
        {
            await _packageDependencyService.AddDependenciesAsync(batch);
        }
    }
}

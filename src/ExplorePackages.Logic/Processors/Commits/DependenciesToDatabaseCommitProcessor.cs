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

        public int BatchSize => 5000;

        public Task<ItemBatch<PackageDependencyGroups>> InitializeItemsAsync(
            IReadOnlyList<PackageEntity> packages,
            int skip,
            CancellationToken token)
        {
            var output = new List<PackageDependencyGroups>();

            foreach (var package in packages)
            {
                InitializeItem(output, package);
            }

            return Task.FromResult(new ItemBatch<PackageDependencyGroups>(
                output,
                hasMoreItems: false));
        }

        private void InitializeItem(List<PackageDependencyGroups> output, PackageEntity package)
        {
            var nuspec = _nuspecStore.GetNuspecContext(package.PackageRegistration.Id, package.Version);
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

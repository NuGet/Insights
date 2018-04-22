using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;

namespace Knapcode.ExplorePackages.Logic
{
    public class DependenciesToDatabaseProcessor : IPackageCommitProcessor<PackageDependencyGroups>
    {
        private readonly NuspecProvider _nuspecProvider;
        private readonly PackageDependencyService _packageDependencyService;

        public DependenciesToDatabaseProcessor(
            NuspecProvider nuspecProvider,
            PackageDependencyService packageDependencyService)
        {
            _nuspecProvider = nuspecProvider;
            _packageDependencyService = packageDependencyService;
        }

        public string CursorName => CursorNames.DependenciesToDatabase;

        public IReadOnlyList<string> DependencyCursorNames { get; } = new[]
        {
            CursorNames.CatalogToNuspecs,
            CursorNames.CatalogToDatabase,
        };

        public Task<PackageDependencyGroups> InitializeItemAsync(PackageEntity package, CancellationToken token)
        {
            var nuspec = _nuspecProvider.GetNuspec(package.PackageRegistration.Id, package.Version);
            if (nuspec.Document == null)
            {
                return Task.FromResult<PackageDependencyGroups>(null);
            }

            var identity = new PackageIdentity(package.PackageRegistration.Id, package.Version);
            var dependencyGroups = NuspecUtility.GetParsedDependencyGroups(nuspec.Document);
            var packageDependencyGroups = new PackageDependencyGroups(identity, dependencyGroups);

            return Task.FromResult(packageDependencyGroups);
        }

        public async Task ProcessBatchAsync(IReadOnlyList<PackageDependencyGroups> batch)
        {
            await _packageDependencyService.AddDependenciesAsync(batch);
        }
    }
}

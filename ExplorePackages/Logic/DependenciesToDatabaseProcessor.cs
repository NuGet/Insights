using System.Collections.Generic;
using System.Linq;
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

        public int BatchSize => 100;

        public async Task<IReadOnlyList<PackageDependencyGroups>> InitializeItemsAsync(IReadOnlyList<PackageEntity> packages, CancellationToken token)
        {
            var output = new List<PackageDependencyGroups>();

            var packageRegistrationKeys = packages
                .Select(x => x.PackageRegistration.PackageRegistrationKey)
                .Distinct()
                .ToList();

            var dependents = await _packageDependencyService.GetDependentPackagesAsync(packageRegistrationKeys);

            foreach (var package in packages.Concat(dependents))
            {
                InitializeItem(output, package);
            }

            return output;
        }

        private void InitializeItem(List<PackageDependencyGroups> output, PackageEntity package)
        {
            var nuspec = _nuspecProvider.GetNuspec(package.PackageRegistration.Id, package.Version);
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Logic
{
    public class DependenciesToDatabaseProcessor : IPackageCommitProcessor<PackageDependencyGroups>
    {
        private readonly NuspecProvider _nuspecProvider;
        private readonly PackageDependencyService _packageDependencyService;
        private readonly ILogger _log;

        public DependenciesToDatabaseProcessor(
            NuspecProvider nuspecProvider,
            PackageDependencyService packageDependencyService,
            ILogger log)
        {
            _nuspecProvider = nuspecProvider;
            _packageDependencyService = packageDependencyService;
            _log = log;
        }

        public string CursorName => CursorNames.DependenciesToDatabase;

        public IReadOnlyList<string> DependencyCursorNames { get; } = new[]
        {
            CursorNames.CatalogToNuspecs,
            CursorNames.CatalogToDatabase,
        };

        public int BatchSize => 25;

        public async Task<IReadOnlyList<PackageDependencyGroups>> InitializeItemsAsync(IReadOnlyList<PackageEntity> packages, CancellationToken token)
        {
            var output = new List<PackageDependencyGroups>();

            var packageRegistrationKeyToId = packages
                .Select(x => x.PackageRegistration)
                .GroupBy(x => x.PackageRegistrationKey, x => x.Id)
                .ToDictionary(x => x.Key, x => x.First());
            var packageRegistrationKeys = packageRegistrationKeyToId.Keys.ToList();

            var dependents = await _packageDependencyService.GetDependentPackagesAsync(packageRegistrationKeys);

            var dependencyGroups = dependents
                .GroupBy(x => x.ParentPackage.PackageKey);

            var dependentPackages = dependencyGroups
                .Select(x => x.First().ParentPackage)
                .ToList();

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

            foreach (var package in packages.Concat(dependentPackages))
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

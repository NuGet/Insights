using System;
using System.Collections.Generic;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageQueryFactory
    {
        private static readonly HashSet<string> BoringQueries = new HashSet<string>
        {
            PackageQueryNames.FindNonNormalizedPackageVersionsNuspecQuery,
            PackageQueryNames.FindMissingDependencyVersionsNuspecQuery,
            PackageQueryNames.FindEmptyDependencyVersionsNuspecQuery,
            PackageQueryNames.FindRepositoriesNuspecQuery,
            PackageQueryNames.FindSemVer2PackageVersionsNuspecQuery,
            PackageQueryNames.FindSemVer2DependencyVersionsNuspecQuery,
        };

        private readonly Func<IEnumerable<IPackageQuery>> _getPackageQueries;
        private readonly IOptionsSnapshot<ExplorePackagesEntitiesSettings> _options;

        public PackageQueryFactory(
            Func<IEnumerable<IPackageQuery>> getPackageQueries,
            IOptionsSnapshot<ExplorePackagesEntitiesSettings> options)
        {
            _getPackageQueries = getPackageQueries;
            _options = options;
        }

        public IReadOnlyList<IPackageQuery> Get()
        {
            var queries = new List<IPackageQuery>();

            foreach (var packageQuery in _getPackageQueries())
            {
                if (!_options.Value.RunBoringQueries && BoringQueries.Contains(packageQuery.Name))
                {
                    continue;
                }

                if (!_options.Value.RunConsistencyChecks && packageQuery is PackageConsistencyPackageQuery)
                {
                    continue;
                }

                queries.Add(packageQuery);
            }

            return queries;
        }
    }
}

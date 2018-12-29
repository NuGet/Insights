using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageQueryFactory
    {
        private static readonly HashSet<Type> BoringQueries = new HashSet<Type>
        {
            typeof(FindNonNormalizedPackageVersionsNuspecQuery),
            typeof(FindMissingDependencyVersionsNuspecQuery),
            typeof(FindEmptyDependencyVersionsNuspecQuery),
        };

        private static readonly HashSet<Type> ConsistencyChecks = new HashSet<Type>
        {
            typeof(HasV2DiscrepancyPackageQuery),
            typeof(HasPackagesContainerDiscrepancyPackageQuery),
            typeof(HasFlatContainerDiscrepancyPackageQuery),
            typeof(HasRegistrationDiscrepancyInOriginalHivePackageQuery),
            typeof(HasRegistrationDiscrepancyInGzippedHivePackageQuery),
            typeof(HasRegistrationDiscrepancyInSemVer2HivePackageQuery),
            typeof(HasSearchDiscrepancyPackageQuery),
            typeof(HasCrossCheckDiscrepancyPackageQuery),
            typeof(HasFlatContainerDiscrepancyPackageQuery),
            typeof(HasFlatContainerDiscrepancyPackageQuery),
        };

        private readonly Func<IEnumerable<IPackageQuery>> _getPackageQueries;
        private readonly IOptionsSnapshot<ExplorePackagesSettings> _settings;

        public PackageQueryFactory(
            Func<IEnumerable<IPackageQuery>> getPackageQueries,
            IOptionsSnapshot<ExplorePackagesSettings> settings)
        {
            _getPackageQueries = getPackageQueries;
            _settings = settings;
        }

        public IReadOnlyList<IPackageQuery> Get()
        {
            var queries = new List<IPackageQuery>();

            foreach (var packageQuery in _getPackageQueries())
            {
                var type = packageQuery.GetType();

                if (!_settings.Value.RunBoringQueries && BoringQueries.Contains(type))
                {
                    continue;
                }

                if (!_settings.Value.RunConsistencyChecks && ConsistencyChecks.Contains(type))
                {
                    continue;
                }

                queries.Add(packageQuery);
            }

            var cursorNameGroups = queries
                .GroupBy(x => x.CursorName)
                .Where(x => x.Count() > 1);

            return queries;
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Entities
{
    public class ProblemService
    {
        private static readonly IReadOnlyList<string> QueryNames = new[]
        {
            PackageQueryNames.HasCrossCheckDiscrepancyPackageQuery,
            PackageQueryNames.HasFlatContainerDiscrepancyPackageQuery,
            PackageQueryNames.HasPackagesContainerDiscrepancyPackageQuery,
            PackageQueryNames.HasRegistrationDiscrepancyInGzippedHivePackageQuery,
            PackageQueryNames.HasRegistrationDiscrepancyInOriginalHivePackageQuery,
            PackageQueryNames.HasRegistrationDiscrepancyInSemVer2HivePackageQuery,
            PackageQueryNames.HasSearchDiscrepancyPackageQuery,
            PackageQueryNames.HasV2DiscrepancyPackageQuery,
            PackageQueryNames.HasInconsistentListedStateQuery,
            PackageQueryNames.IsMissingFromCatalogQuery,
        };

        private readonly PackageQueryService _packageQueryService;
        private readonly EntityContextFactory _entityContextFactory;
        private readonly ILogger<ProblemService> _logger;

        public ProblemService(
            PackageQueryService packageQueryService,
            EntityContextFactory entityContextFactory,
            ILogger<ProblemService> logger)
        {
            _packageQueryService = packageQueryService;
            _entityContextFactory = entityContextFactory;
            _logger = logger;
        }

        public IReadOnlyList<string> ProblemQueryNames => QueryNames;

        public async Task<IReadOnlyList<Problem>> GetProblemsAsync()
        {
            var problems = new List<Problem>();

            foreach (var queryName in QueryNames)
            {
                _logger.LogInformation("Getting results for package query {QueryName}.", queryName);
                var matches = await _packageQueryService.GetAllMatchedPackagesAsync(queryName);
                problems.AddRange(matches.Select(x => new Problem(new PackageIdentity(x.Id, x.Version), queryName)));
            }

            return problems;
        }
    }
}

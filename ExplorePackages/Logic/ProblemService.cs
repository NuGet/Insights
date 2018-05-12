using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic
{
    public class ProblemService
    {
        private static IReadOnlyList<string> QueryNames = new[]
        {
            PackageQueryNames.HasCrossCheckDiscrepancyPackageQuery,
            PackageQueryNames.HasFlatContainerDiscrepancyPackageQuery,
            PackageQueryNames.HasPackagesContainerDiscrepancyPackageQuery,
            PackageQueryNames.HasRegistrationDiscrepancyInGzippedHivePackageQuery,
            PackageQueryNames.HasRegistrationDiscrepancyInOriginalHivePackageQuery,
            PackageQueryNames.HasRegistrationDiscrepancyInSemVer2HivePackageQuery,
            PackageQueryNames.HasSearchDiscrepancyPackageQuery,
            PackageQueryNames.HasV2DiscrepancyPackageQuery,
        };

        private const string ListedInV2 = "PackageListedInV2ButNotInCatalog";
        private const string ListedInCatalog = "PackageListedInCatalogButNotInV2";
        private const string MissingFromCatalog = "PackageMissingFromCatalog";

        private const string MismatchingListedStatusQuery = @"
            SELECT pr.Id, p.Version, v2.Listed AS V2Listed, cp.Listed AS CatalogListed
            FROM Packages p
            INNER JOIN PackageRegistrations pr ON pr.PackageRegistrationKey = p.PackageRegistrationKey
            INNER JOIN V2Packages v2 ON p.PackageKey = v2.PackageKey
            INNER JOIN CatalogPackages cp ON cp.PackageKey = v2.PackageKey
            WHERE v2.Listed <> cp.Listed";

        private const string MissingFromCatalogQuery = @"
            SELECT pr.Id, p.Version
            FROM V2Packages v2
            INNER JOIN PackageRegistrations pr ON pr.PackageRegistrationKey = p.PackageRegistrationKey
            INNER JOIN Packages p ON v2.PackageKey = p.PackageKey
            LEFT OUTER JOIN CatalogPackages cp ON v2.PackageKey = cp.PackageKey
            WHERE cp.PackageKey IS NULL";

        private readonly PackageQueryService _packageQueryService;
        private readonly ILogger<ProblemService> _logger;

        public ProblemService(PackageQueryService packageQueryService, ILogger<ProblemService> logger)
        {
            _packageQueryService = packageQueryService;
            _logger = logger;
        }

        public async Task<IReadOnlyList<Problem>> GetProblemsAsync()
        {
            var problems = new List<Problem>();

            // Get package query results.
            foreach (var queryName in QueryNames)
            {
                _logger.LogInformation("Getting results for package query {QueryName}.", queryName);
                var matches = await _packageQueryService.GetAllMatchedPackagesAsync(queryName);
                problems.AddRange(matches.Select(x => new Problem(new PackageIdentity(x.Id, x.Version), queryName)));
            }

            // Find listed status inconsistencies.
            _logger.LogInformation("Getting mismatched listed status.");
            var mismatchingListedStatusRecords = await ReadQueryResultsAsync(
                MismatchingListedStatusQuery,
                x => new MismatchingListedStatusRecord(
                    x.GetString(0),
                    x.GetString(1),
                    x.GetBoolean(2),
                    x.GetBoolean(3)));
            foreach (var record in mismatchingListedStatusRecords)
            {
                var identity = new PackageIdentity(record.Id, record.Version);
                problems.Add(new Problem(identity, record.V2Listed ? ListedInV2 : ListedInCatalog));
            }

            // Find packages missing from the catalog.
            _logger.LogInformation("Getting packages missing from catalog.");
            var missingFromCatalogIdentities = await ReadQueryResultsAsync(
                MissingFromCatalogQuery,
                x => new PackageIdentity(
                    x.GetString(0),
                    x.GetString(1)));
            foreach (var identity in missingFromCatalogIdentities)
            {
                problems.Add(new Problem(identity, MissingFromCatalog));
            }

            return problems;
        }

        private static async Task<IReadOnlyList<T>> ReadQueryResultsAsync<T>(string query, Func<DbDataReader, T> readRecord)
        {
            using (var context = new EntityContext())
            using (var connection = context.Database.GetDbConnection())
            using (var command = connection.CreateCommand())
            {
                await connection.OpenAsync();
                command.CommandText = query;

                using (var reader = await command.ExecuteReaderAsync())
                {
                    var output = new List<T>();
                    while (await reader.ReadAsync())
                    {
                        output.Add(readRecord(reader));
                    }

                    return output;
                }
            }
        }

        private class MismatchingListedStatusRecord
        {
            public MismatchingListedStatusRecord(string id, string version, bool v2Listed, bool catalogListed)
            {
                Id = id;
                Version = version;
                V2Listed = v2Listed;
                CatalogListed = catalogListed;
            }

            public string Id { get; }
            public string Version { get; }
            public bool V2Listed { get; }
            public bool CatalogListed { get; }
        }
    }
}

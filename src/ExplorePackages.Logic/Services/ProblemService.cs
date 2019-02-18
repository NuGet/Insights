using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
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
            WHERE v2.Listed <> cp.Listed
              AND v2.LastEditedTimestamp < @MaxTimestamp
              AND cp.LastCommitTimestamp < @MaxTimestamp
              AND p.PackageKey > @MinPackageKey
              AND p.PackageKey <= @MaxPackageKey";

        private const string MissingFromCatalogQuery = @"
            SELECT pr.Id, p.Version
            FROM Packages p
            INNER JOIN V2Packages v2 ON v2.PackageKey = p.PackageKey
            INNER JOIN PackageRegistrations pr ON pr.PackageRegistrationKey = p.PackageRegistrationKey
            LEFT OUTER JOIN CatalogPackages cp ON v2.PackageKey = cp.PackageKey
            WHERE cp.PackageKey IS NULL
              AND v2.CreatedTimestamp < @MaxTimestamp
              AND p.PackageKey > @MinPackageKey
              AND p.PackageKey <= @MaxPackageKey";

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

            // Get package query results.
            foreach (var queryName in QueryNames)
            {
                _logger.LogInformation("Getting results for package query {QueryName}.", queryName);
                var matches = await _packageQueryService.GetAllMatchedPackagesAsync(queryName);
                problems.AddRange(matches.Select(x => new Problem(new PackageIdentity(x.Id, x.Version), queryName)));
            }

            // Execute other consistency check queries.
            _logger.LogInformation("Getting packages missing from catalog.");
            using (var context = await _entityContextFactory.GetAsync())
            using (var connection = context.Database.GetDbConnection())
            {
                await connection.OpenAsync();

                long minPackageKey = 0;
                var batchSize = 10000;
                var hasMoreRows = true;
                var maxTimestamp = DateTimeOffset.UtcNow.AddHours(-1);
                while (hasMoreRows)
                {
                    var maxPackageKey = minPackageKey + batchSize;
                    var stopwatch = Stopwatch.StartNew();

                    hasMoreRows = await context
                        .Packages
                        .Where(x => x.PackageKey > maxPackageKey)
                        .AnyAsync();

                    // Find mismatch listed status.
                    var mismatchingListedStatusRecords = await ReadQueryResultsAsync(
                        connection,
                        MismatchingListedStatusQuery,
                        x => AddBoundParameters(x, minPackageKey, batchSize, maxTimestamp),
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
                    var missingFromCatalogIdentities = await ReadQueryResultsAsync(
                        connection,
                        MissingFromCatalogQuery,
                        x => AddBoundParameters(x, minPackageKey, batchSize, maxTimestamp),
                        x => new PackageIdentity(x.GetString(0), x.GetString(1)));
                    foreach (var identity in missingFromCatalogIdentities)
                    {
                        problems.Add(new Problem(identity, MissingFromCatalog));
                    }

                    minPackageKey += batchSize;

                    _logger.LogInformation(
                        "Checked package keys > {MinPackageKey} and <= {MaxPackageKey}. {Duration}ms",
                        minPackageKey,
                        maxPackageKey,
                        stopwatch.ElapsedMilliseconds);
                }
            }

            return problems;
        }

        private static void AddBoundParameters(DbCommand command, long minPackageKey, int batchSize, DateTimeOffset maxTimestamp)
        {
            var maxTimestampParameter = command.CreateParameter();
            maxTimestampParameter.ParameterName = "MaxTimestamp";
            maxTimestampParameter.Value = maxTimestamp.UtcTicks;
            maxTimestampParameter.DbType = DbType.Int64;
            command.Parameters.Add(maxTimestampParameter);

            var minPackageKeyParameter = command.CreateParameter();
            minPackageKeyParameter.ParameterName = "MinPackageKey";
            minPackageKeyParameter.Value = minPackageKey;
            minPackageKeyParameter.DbType = DbType.Int64;
            command.Parameters.Add(minPackageKeyParameter);

            var maxPackageKeyParameter = command.CreateParameter();
            maxPackageKeyParameter.ParameterName = "MaxPackageKey";
            maxPackageKeyParameter.Value = minPackageKey + batchSize;
            maxPackageKeyParameter.DbType = DbType.Int64;
            command.Parameters.Add(maxPackageKeyParameter);
        }

        private async Task<IReadOnlyList<T>> ReadQueryResultsAsync<T>(
            DbConnection connection,
            string query,
            Action<DbCommand> configureCommand,
            Func<DbDataReader, T> readRecord)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = query;
                configureCommand(command);

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

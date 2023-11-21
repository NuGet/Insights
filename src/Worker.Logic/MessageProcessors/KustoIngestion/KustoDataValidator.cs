// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kusto.Data.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using NuGet.Insights.Worker.CatalogDataToCsv;

#if ENABLE_CRYPTOAPI
using NuGet.Insights.Worker.PackageCertificateToCsv;
using NuGet.Insights.Worker.PackageSignatureToCsv;
#endif

namespace NuGet.Insights.Worker.KustoIngestion
{
    public class KustoDataValidator
    {
        private readonly ICslQueryProvider _queryProvider;
        private readonly IReadOnlyDictionary<Type, ICsvRecordStorage> _typeToStorage;
        private readonly CsvRecordContainers _containers;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ITelemetryClient _telemetryClient;
        private readonly ILogger<KustoDataValidator> _logger;

        public KustoDataValidator(
            ICslQueryProvider queryProvider,
            IEnumerable<ICsvRecordStorage> csvResultStorage,
            CsvRecordContainers containers,
            IOptions<NuGetInsightsWorkerSettings> options,
            ITelemetryClient telemetryClient,
            ILogger<KustoDataValidator> logger)
        {
            _queryProvider = queryProvider;
            _typeToStorage = csvResultStorage.ToDictionary(x => x.RecordType);
            _containers = containers;
            _options = options;
            _telemetryClient = telemetryClient;
            _logger = logger;
        }

        public async Task<bool?> IsIngestedDataNewerAsync()
        {
            var tableNames = await GetTableNamesAsync();

            if (_typeToStorage.TryGetValue(typeof(CatalogLeafItemRecord), out var storage))
            {
                var newTable = _containers.GetTempKustoTableName(storage.ContainerName);
                var existingTable = _containers.GetKustoTableName(storage.ContainerName);

                if (tableNames.Contains(existingTable) && tableNames.Contains(newTable))
                {
                    return await DoesTableHaveNewerDataAsync(newTable, existingTable, nameof(CatalogLeafItemRecord.CommitTimestamp));
                }
            }

            var recordTypesWithCatalogCommitTimestamp = _typeToStorage
                .Where(x => x.Value.RecordType.GetProperty(nameof(PackageRecord.CatalogCommitTimestamp)) != null)
                .Select(x => x.Key);

            foreach (var recordType in recordTypesWithCatalogCommitTimestamp)
            {
                storage = _typeToStorage[recordType];
                var newTable = _containers.GetTempKustoTableName(storage.ContainerName);
                var existingTable = _containers.GetKustoTableName(storage.ContainerName);

                if (tableNames.Contains(existingTable) && tableNames.Contains(newTable))
                {
                    return await DoesTableHaveNewerDataAsync(newTable, existingTable, nameof(PackageRecord.CatalogCommitTimestamp));
                }
            }

            return null;
        }

        private async Task<bool> DoesTableHaveNewerDataAsync(string newTable, string existingTable, string column)
        {
            var clientRequestId = Guid.NewGuid().ToString();
            _logger.LogInformation(
                "Getting max {Column} timestamp values in table {NewTable} and {ExistingTable} with client request ID {ClientRequestId}.",
                column,
                newTable,
                existingTable,
                clientRequestId);

            var query =
                $"""          
                {existingTable}
                | summarize MaxCommitTimestamp = max({column})
                | extend MaxCommitTimestamp = iff(isempty(MaxCommitTimestamp), todatetime("2000-01-10"), MaxCommitTimestamp)
                | project TableName = "{existingTable}", MaxCommitTimestamp
                | union (
                    {newTable}
                    | summarize MaxCommitTimestamp = max({column})
                    | extend MaxCommitTimestamp = iff(isempty(MaxCommitTimestamp), todatetime("2000-01-10"), MaxCommitTimestamp)
                    | project TableName = "{newTable}", MaxCommitTimestamp
                )
                """;

            using var dataReader = await _queryProvider.ExecuteQueryAsync(
                _options.Value.KustoDatabaseName,
                query,
                new ClientRequestProperties { ClientRequestId = clientRequestId });

            var now = DateTime.UtcNow;

            var tableNameToMaxCommitTimestamp = new Dictionary<string, DateTime>();
            while (dataReader.Read())
            {
                var tableName = dataReader.GetString(0);
                var maxCommitTimestamp = dataReader.GetDateTime(1);

                _telemetryClient.TrackMetric(
                    nameof(KustoDataValidator) + ".CommitTimestampAgeInHours",
                    (now - maxCommitTimestamp).TotalHours,
                    new Dictionary<string, string>
                    {
                        { "Table", tableName },
                        { "Column", column },
                    });

                tableNameToMaxCommitTimestamp.Add(tableName, maxCommitTimestamp);
            }

            var newTableValue = tableNameToMaxCommitTimestamp[newTable];
            var existingTableValue = tableNameToMaxCommitTimestamp[existingTable];

            _telemetryClient.TrackMetric(
                nameof(KustoDataValidator) + ".MaxCommitTimestampDeltaInHours",
                (newTableValue - existingTableValue).TotalHours,
                new Dictionary<string, string>
                {
                    { "NewTable", newTable },
                    { "ExistingTable", existingTable },
                    { "Column", column },
                });

            return newTableValue > existingTableValue;
        }

        private async Task<IReadOnlySet<string>> GetTableNamesAsync()
        {
            var clientRequestId = Guid.NewGuid().ToString();
            _logger.LogInformation(
                "Getting table list in Kusto with client request ID {ClientRequestId}.",
                clientRequestId);

            using var dataReader = await _queryProvider.ExecuteQueryAsync(
                _options.Value.KustoDatabaseName,
                ".show tables | project TableName",
                new ClientRequestProperties { ClientRequestId = clientRequestId });

            var tableNames = new HashSet<string>();
            while (dataReader.Read())
            {
                tableNames.Add(dataReader.GetString(0));
            }

            return tableNames;
        }

        public async Task<bool> ValidateAsync()
        {
            var validations = new ConcurrentBag<Validation>(Enumerable
                .Empty<Validation>()
#if ENABLE_CRYPTOAPI
                .Concat(await GetFingerprintValidationsAsync())
                .Concat(await GetFingerprintSHA256ValidationsAsync())
#endif
                .Concat(await GetIdentityValidationsAsync())
                .Concat(await GetLowerIdValidationsAsync()));

            var failures = 0;

            await Task.WhenAll(Enumerable
                .Range(0, 8)
                .Select(async x =>
                {
                    while (validations.TryTake(out var validation))
                    {
                        var clientRequestId = Guid.NewGuid().ToString();
                        _logger.LogInformation(
                            "Executing {ValidationLabel} in Kusto with client request ID {ClientRequestId}.",
                            validation.Label,
                            clientRequestId);
                        var stopwatch = Stopwatch.StartNew();
                        using var dataReader = await _queryProvider.ExecuteQueryAsync(
                            _options.Value.KustoDatabaseName,
                            validation.Query,
                            new ClientRequestProperties { ClientRequestId = clientRequestId });
                        stopwatch.Stop();
                        _logger.LogInformation(
                            "Completed {ValidationLabel} in Kusto with client request ID {ClientRequestId} in {ElapsedMs}ms.",
                            validation.Label,
                            clientRequestId,
                            stopwatch.ElapsedMilliseconds);

                        var errorMessage = validation.Validate(dataReader);

                        _telemetryClient.TrackMetric(
                            $"{nameof(KustoDataValidator)}.{nameof(ValidateAsync)}.DurationMs",
                            stopwatch.ElapsedMilliseconds,
                            new Dictionary<string, string>
                            {
                                { "ValidationLabel", validation.Label },
                                { "Success", errorMessage == null ? "true" : "false" },
                            });

                        if (errorMessage != null)
                        {
                            _logger.LogError(
                                $"A Kusto validation query failed.{Environment.NewLine}" +
                                $"Validation label: {{ValidationLabel}}{Environment.NewLine}" +
                                $"Error: {{ErrorMessage}}{Environment.NewLine}" +
                                $"{{Query}}",
                                validation.Label,
                                errorMessage,
                                validation.Query);
                            Interlocked.Increment(ref failures);
                        }
                    }
                })
                .ToList());

            return failures == 0;
        }

        private async Task<IReadOnlyList<Validation>> GetIdentityValidationsAsync()
        {
            return await GetSetValidationsAsync(nameof(PackageRecord.Identity), required: true);
        }

        private async Task<IReadOnlyList<Validation>> GetLowerIdValidationsAsync()
        {
            return await GetSetValidationsAsync(nameof(PackageRecord.LowerId), required: true);
        }

#if ENABLE_CRYPTOAPI
        private async Task<IReadOnlyList<Validation>> GetFingerprintValidationsAsync()
        {
            return await GetSetValidationsAsync(nameof(CertificateRecord.Fingerprint), required: false);
        }

        private async Task<IReadOnlyList<Validation>> GetFingerprintSHA256ValidationsAsync()
        {
            var packageSignatureStorage = _typeToStorage[typeof(PackageSignature)];
            var certificatesStorage = _typeToStorage[typeof(CertificateRecord)];
            if (!await HasBlobsAsync(packageSignatureStorage) || !await HasBlobsAsync(certificatesStorage))
            {
                return Array.Empty<Validation>();
            }

            var leftTable = _containers.GetTempKustoTableName(packageSignatureStorage.ContainerName);
            var rightTable = _containers.GetTempKustoTableName(certificatesStorage.ContainerName);
            var column = nameof(CertificateRecord.FingerprintSHA256Hex);
            var joinQuery = @$"{leftTable}
| mv-expand {column} = pack_array(
    AuthorSHA256,
    AuthorTimestampSHA256,
    RepositorySHA256,
    RepositoryTimestampSHA256) to typeof(string)
| where isnotempty({column})
| distinct {column}
| join kind=leftouter {rightTable}";

            return new[] { GetLeftRightValidation(leftTable, rightTable, column, "left outer", joinQuery) };
        }
#endif

        private async Task<bool> HasBlobsAsync(ICsvRecordStorage storage)
        {
            var blobs = await _containers.GetBlobsAsync(storage.ContainerName);
            return blobs.Count > 0;
        }

        private async Task<IReadOnlyList<Validation>> GetSetValidationsAsync(string column, bool required)
        {
            var storageWithBlobs = new List<ICsvRecordStorage>();
            foreach (var storage in _typeToStorage.Values)
            {
                if (await HasBlobsAsync(storage))
                {
                    storageWithBlobs.Add(storage);
                }
            }

            var tables = storageWithBlobs
                .Where(x => x.RecordType.GetProperty(column) != null)
                .Select(x => _containers.GetTempKustoTableName(x.ContainerName))
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            if (tables.Count <= 1)
            {
                return Array.Empty<Validation>();
            }

            var leftTable = tables.First();
            var validations = new List<Validation>();

            foreach (var rightTable in tables.Skip(1))
            {
                var joinQuery = @$"{leftTable}{(required ? string.Empty : $"{Environment.NewLine}| where isnotempty({column})")}
| distinct {column}
| join kind=fullouter (
    {rightTable}{(required ? string.Empty : $"{Environment.NewLine}    | where isnotempty({column})")}
    | distinct {column}
)";

                validations.Add(GetLeftRightValidation(leftTable, rightTable, column, "full outer", joinQuery));
            }

            return validations;
        }

        private static Validation GetLeftRightValidation(string leftTable, string rightTable, string column, string comparisonType, string joinQuery)
        {
            var query = $@"{joinQuery} on {column}
| where isempty({column}) or isempty({column}1)
| summarize
    LeftOnlyCount = countif(isnotempty({column})),
    LeftOnlySample = make_set_if({column}, isnotempty({column}), 5),
    RightOnlyCount = countif(isnotempty({column}1)),
    RightOnlySample = make_set_if({column}1, isnotempty({column}1), 5)";

            return new Validation(
                $"{comparisonType} set comparison of {leftTable}.{column} and {rightTable}.{column}",
                query,
                dataReader =>
                {
                    if (!dataReader.Read())
                    {
                        return "The query did not return any rows.";
                    }

                    var leftOnlyCount = dataReader.GetInt64(0);
                    var leftOnlySample = (JToken)dataReader.GetValue(1);
                    var rightOnlyCount = dataReader.GetInt64(2);
                    var rightOnlySample = (JToken)dataReader.GetValue(3);

                    if (leftOnlyCount != 0 || rightOnlyCount != 0)
                    {
                        return
                            $"The set of values in the {column} columns in the {leftTable} and {rightTable} tables do not match.{Environment.NewLine}" +
                            $"{column} values in {leftTable} but not {rightTable}:{Environment.NewLine}" +
                            $"  - Count: {leftOnlyCount}{Environment.NewLine}" +
                            $"  - Sample: {leftOnlySample}{Environment.NewLine}" +
                            $"{column} values in {rightTable} but not {leftTable}:{Environment.NewLine}" +
                            $"  - Count: {rightOnlyCount}{Environment.NewLine}" +
                            $"  - Sample: {rightOnlySample}{Environment.NewLine}";
                    }

                    if (dataReader.Read())
                    {
                        return "The query returned more than one row.";
                    }

                    return null;
                });
        }

        private record Validation(string Label, string Query, Func<IDataReader, string> Validate);
    }
}

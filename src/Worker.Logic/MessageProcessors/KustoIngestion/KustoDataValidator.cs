// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Kusto.Data.Common;
using NuGet.Insights.Kusto;
using NuGet.Insights.Worker.CatalogDataToCsv;

namespace NuGet.Insights.Worker.KustoIngestion
{
    public class KustoDataValidator
    {
        private readonly CachingKustoClientFactory _kustoClientFactory;
        private readonly IReadOnlyList<IKustoValidationProvider> _validationProviders;
        private readonly CsvRecordContainers _containers;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ITelemetryClient _telemetryClient;
        private readonly ILogger<KustoDataValidator> _logger;

        public KustoDataValidator(
            CachingKustoClientFactory kustoClientFactory,
            IEnumerable<IKustoValidationProvider> validationProviders,
            CsvRecordContainers containers,
            IOptions<NuGetInsightsWorkerSettings> options,
            ITelemetryClient telemetryClient,
            ILogger<KustoDataValidator> logger)
        {
            _kustoClientFactory = kustoClientFactory;
            _validationProviders = validationProviders.ToList();
            _containers = containers;
            _options = options;
            _telemetryClient = telemetryClient;
            _logger = logger;
        }

        public async Task<bool?> IsIngestedDataNewerAsync()
        {
            var tableNames = await GetTableNamesAsync();

            if (_containers.TryGetInfoByRecordType<CatalogLeafItemRecord>(out var catalogLeafItemInfo))
            {
                var newTable = _containers.GetTempKustoTableName(catalogLeafItemInfo.ContainerName);
                var existingTable = _containers.GetKustoTableName(catalogLeafItemInfo.ContainerName);

                if (tableNames.Contains(existingTable) && tableNames.Contains(newTable))
                {
                    return await DoesTableHaveNewerDataAsync(newTable, existingTable, nameof(CatalogLeafItemRecord.CommitTimestamp));
                }
            }

            var containersWithCatalogCommitTimestamp = _containers
                .ContainerInfo
                .Where(x => x.RecordType.GetProperty(nameof(PackageRecord.CatalogCommitTimestamp)) != null);

            foreach (var info in containersWithCatalogCommitTimestamp)
            {
                var newTable = _containers.GetTempKustoTableName(info.ContainerName);
                var existingTable = _containers.GetKustoTableName(info.ContainerName);

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

            var queryClient = await _kustoClientFactory.GetQueryClientAsync();
            using var dataReader = await queryClient.ExecuteQueryAsync(
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

            var queryClient = await _kustoClientFactory.GetQueryClientAsync();
            using var dataReader = await queryClient.ExecuteQueryAsync(
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
            var validations = new ConcurrentBag<KustoValidation>();
            foreach (var provider in _validationProviders)
            {
                foreach (var validation in await provider.GetValidationsAsync())
                {
                    validations.Add(validation);
                }
            }

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
                        var queryClient = await _kustoClientFactory.GetQueryClientAsync();
                        using var dataReader = await queryClient.ExecuteQueryAsync(
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
    }
}

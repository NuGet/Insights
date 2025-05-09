// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Data.Tables;
using Kusto.Data.Common;
using Kusto.Ingest;
using NuGet.Insights.Kusto;
using NuGet.Insights.StorageNoOpRetry;

namespace NuGet.Insights.Worker.KustoIngestion
{
    public class KustoBlobIngestionMessageProcessor : IMessageProcessor<KustoBlobIngestionMessage>
    {
        private readonly KustoIngestionStorageService _storageService;
        private readonly AutoRenewingStorageLeaseService _leaseService;
        private readonly CsvRecordContainers _csvRecordContainers;
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly CachingKustoClientFactory _kustoClientFactory;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly ITelemetryClient _telemetryClient;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ILogger<KustoBlobIngestionMessageProcessor> _logger;

        public KustoBlobIngestionMessageProcessor(
            KustoIngestionStorageService storageService,
            AutoRenewingStorageLeaseService leaseService,
            CsvRecordContainers csvRecordContainers,
            ServiceClientFactory serviceClientFactory,
            CachingKustoClientFactory kustoClientFactory,
            IMessageEnqueuer messageEnqueuer,
            ITelemetryClient telemetryClient,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger<KustoBlobIngestionMessageProcessor> logger)
        {
            _storageService = storageService;
            _leaseService = leaseService;
            _csvRecordContainers = csvRecordContainers;
            _serviceClientFactory = serviceClientFactory;
            _kustoClientFactory = kustoClientFactory;
            _messageEnqueuer = messageEnqueuer;
            _telemetryClient = telemetryClient;
            _options = options;
            _logger = logger;
        }

        public async Task ProcessAsync(KustoBlobIngestionMessage message, long dequeueCount)
        {
            var blob = await _storageService.GetBlobAsync(message.StorageSuffix, message.ContainerName, message.BlobName);
            if (blob == null)
            {
                _logger.LogTransientWarning("No matching Kusto blob ingestion was found.");
                return;
            }

            if (blob.State == KustoBlobIngestionState.Created)
            {
                var blobUrlWithSas = await _serviceClientFactory.GetBlobReadUrlAsync(_options.Value, blob.ContainerName, blob.BlobName);

                var tempTableName = _csvRecordContainers.GetTempKustoTableName(blob.ContainerName);
                var ingestionProperties = new KustoQueuedIngestionProperties(_options.Value.KustoDatabaseName, tempTableName)
                {
                    IgnoreFirstRecord = true,
                    Format = DataSourceFormat.csv,
                    IngestionMapping = new IngestionMapping
                    {
                        IngestionMappingReference = NuGetInsightsWorkerLogicKustoDDL.CsvMappingName
                    },
                    ReportLevel = IngestionReportLevel.FailuresAndSuccesses,
                    ReportMethod = IngestionReportMethod.Table,
                };

                var sourceOptions = new StorageSourceOptions
                {
                    SourceId = blob.SourceId,
                    Size = blob.RawSizeBytes,
                };

                await using var lease = await _leaseService.TryAcquireAsync($"KustoBlobIngestion-{blob.ContainerName}-{blob.BlobName}");
                if (!lease.Acquired)
                {
                    _logger.LogTransientWarning(
                        "Kusto blob ingestion lease for blob {ContainerName}/{BlobName} is not available.",
                        blob.ContainerName,
                        blob.BlobName);
                    message.AttemptCount++;
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount, factor: 10));
                    return;
                }

                _logger.LogInformation(
                    "Starting ingestion of blob {ContainerName}/{BlobName} into Kusto table {KustoTable}.",
                    blob.ContainerName,
                    blob.BlobName,
                    tempTableName);
                var ingestClient = await _kustoClientFactory.GetIngestClientAsync();
                var result = await ingestClient.IngestFromStorageAsync(
                    blobUrlWithSas.AbsoluteUri,
                    ingestionProperties,
                    sourceOptions);

                // To make the queued ingestion result queryable across method invocations, we must do some dirty hacks.
                // We need to capture the "IngestionStatusTable" property on the "TableReportIngestionResult" class
                // returned here. We capture the table URL and SAS so that we can poll the ingestion status later.
                var tableProperty = result.GetType().GetProperty("IngestionStatusTable");
                var table = tableProperty.GetValue(result);
                var tableSasUriProperty = table.GetType().GetProperty("TableSasUri");
                var tableSasUri = (string)tableSasUriProperty.GetValue(table);

                blob.StatusUrl = tableSasUri;
                blob.State = KustoBlobIngestionState.Working;
                blob.Started = DateTimeOffset.UtcNow;
                await _storageService.ReplaceBlobAsync(blob);
            }

            if (blob.State == KustoBlobIngestionState.Working)
            {
                var statusList = await GetIngestionStatusListAsync(blob);
                var statusSummary = statusList
                    .GroupBy(x => x.Status)
                    .OrderBy(x => x.Key.ToString(), StringComparer.Ordinal)
                    .Select(x => $"{x.Key} ({x.Count()}x)")
                    .ToList();
                _logger.LogInformation("Ingestion status: {Statuses}", statusSummary);

                var duration = DateTimeOffset.UtcNow - blob.Started.Value;

                if (statusSummary.Count > 1)
                {
                    _telemetryClient.TrackMetric(
                        nameof(KustoBlobIngestionMessageProcessor) + ".MixedStatus.ElapsedMs",
                        duration.TotalMilliseconds,
                        new Dictionary<string, string>
                        {
                            { "ContainerName", blob.ContainerName },
                            { "BlobName", blob.BlobName },
                            { "SourceId", blob.SourceId.ToString() },
                            { "StatusSummary", string.Join(", ", statusSummary) },
                        });
                }

                if (duration > _options.Value.KustoBlobIngestionTimeout)
                {
                    _telemetryClient.TrackMetric(
                        nameof(KustoBlobIngestionMessageProcessor) + ".TimedOut.ElapsedMs",
                        duration.TotalMilliseconds,
                        new Dictionary<string, string>
                        {
                            { "ContainerName", blob.ContainerName },
                            { "BlobName", blob.BlobName },
                            { "SourceId", blob.SourceId.ToString() },
                        });

                    _logger.LogWarning(
                        "The ingestion of blob {ContainerName}{BlobName} timed out after {Duration}.",
                        blob.ContainerName,
                        blob.BlobName,
                        duration);

                    blob.State = KustoBlobIngestionState.TimedOut;
                    await _storageService.ReplaceBlobAsync(blob);
                }
                else if (statusList.Any(x => x.Status == Status.Pending))
                {
                    message.AttemptCount++;
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount, factor: 10));
                    return;
                }
                else if (statusList.Any(x => x.Status != Status.Succeeded))
                {
                    _telemetryClient.TrackMetric(
                        nameof(KustoBlobIngestionMessageProcessor) + ".Failed.ElapsedMs",
                        duration.TotalMilliseconds,
                        new Dictionary<string, string>
                        {
                            { "ContainerName", blob.ContainerName },
                            { "BlobName", blob.BlobName },
                            { "SourceId", blob.SourceId.ToString() },
                            { "StatusSummary", string.Join(", ", statusSummary) },
                        });

                    _logger.LogWarning(
                        "The ingestion of blob {ContainerName}{BlobName} did not succeed. The statuses were: {StatusSummary}",
                        blob.ContainerName,
                        blob.BlobName,
                        statusSummary);

                    blob.State = KustoBlobIngestionState.Failed;
                    await _storageService.ReplaceBlobAsync(blob);
                }
                else
                {
                    _telemetryClient.TrackMetric(
                        nameof(KustoBlobIngestionMessageProcessor) + ".Complete.ElapsedMs",
                        duration.TotalMilliseconds);

                    _logger.LogInformation(
                        "The ingestion of blob {ContainerName}{BlobName} is complete.",
                        blob.ContainerName,
                        blob.BlobName);
                    await _storageService.DeleteBlobAsync(blob);
                }
            }
        }

        private async Task<List<IngestionStatus>> GetIngestionStatusListAsync(KustoBlobIngestion blob)
        {
            using var metrics = _telemetryClient.StartQueryLoopMetrics();

            var tableUrl = new Uri(blob.StatusUrl);
            TableClientWithRetryContext statusTable;
            if (tableUrl.Query is not null && tableUrl.Query.Contains("sig=", StringComparison.Ordinal))
            {
                statusTable = new TableClientWithRetryContext(new TableClient(tableUrl), _telemetryClient);
            }
            else
            {
                // This should only happen in mock Kusto tests where a real table SAS URL may not be available.
                var tableName = new string(tableUrl
                    .AbsolutePath
                    .Split('/', StringSplitOptions.RemoveEmptyEntries)
                    .Last()
                    .TakeWhile(char.IsAsciiLetterOrDigit).ToArray());
                var tableClient = await _serviceClientFactory.GetTableServiceClientAsync(_options.Value);
                statusTable = tableClient.GetTableClient(tableName);
            }

            _logger.LogInformation(
                "Checking ingestion status of blob {ContainerName}/{BlobName} in status table {StatusTable}.",
                blob.ContainerName,
                blob.BlobName,
                tableUrl.GetLeftPart(UriPartial.Path));

            var partitionKey = blob.SourceId.ToString();
            var statusList = await statusTable
                .QueryAsync<IngestionStatus>(s => s.PartitionKey == partitionKey)
                .ToListAsync(_telemetryClient.StartQueryLoopMetrics());
            _logger.LogInformation("Fetched {Count} ingestion status records.", statusList.Count);

            return statusList;
        }
    }
}

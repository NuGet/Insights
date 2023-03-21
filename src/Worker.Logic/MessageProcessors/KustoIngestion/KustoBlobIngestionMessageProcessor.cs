// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kusto.Data.Common;
using Kusto.Ingest;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage.Table;

namespace NuGet.Insights.Worker.KustoIngestion
{
    public class KustoBlobIngestionMessageProcessor : IMessageProcessor<KustoBlobIngestionMessage>
    {
        private readonly KustoIngestionStorageService _storageService;
        private readonly AutoRenewingStorageLeaseService _leaseService;
        private readonly CsvResultStorageContainers _csvRecordContainers;
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IKustoQueuedIngestClient _kustoQueuedIngestClient;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly ITelemetryClient _telemetryClient;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ILogger<KustoBlobIngestionMessageProcessor> _logger;

        public KustoBlobIngestionMessageProcessor(
            KustoIngestionStorageService storageService,
            AutoRenewingStorageLeaseService leaseService,
            CsvResultStorageContainers csvRecordContainers,
            ServiceClientFactory serviceClientFactory,
            IKustoQueuedIngestClient kustoQueuedIngestClient,
            IMessageEnqueuer messageEnqueuer,
            ITelemetryClient telemetryClient,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger<KustoBlobIngestionMessageProcessor> logger)
        {
            _storageService = storageService;
            _leaseService = leaseService;
            _csvRecordContainers = csvRecordContainers;
            _serviceClientFactory = serviceClientFactory;
            _kustoQueuedIngestClient = kustoQueuedIngestClient;
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
                _logger.LogWarning("No matching Kusto blob ingestion was found.");
                return;
            }

            if (blob.State == KustoBlobIngestionState.Created)
            {
                var blobUrlWithSas = await _serviceClientFactory.GetBlobReadUrlAsync(blob.GetContainerName(), blob.GetBlobName());

                var tempTableName = _csvRecordContainers.GetTempKustoTableName(blob.GetContainerName());
                var ingestionProperties = new KustoQueuedIngestionProperties(_options.Value.KustoDatabaseName, tempTableName)
                {
                    IgnoreFirstRecord = true,
                    Format = DataSourceFormat.csv,
                    IngestionMapping = new IngestionMapping
                    {
                        IngestionMappingReference = KustoDDL.CsvMappingName
                    },
                    ReportLevel = IngestionReportLevel.FailuresAndSuccesses,
                    ReportMethod = IngestionReportMethod.Table,
                };

                var sourceOptions = new StorageSourceOptions
                {
                    SourceId = blob.SourceId,
                    Size = blob.RawSizeBytes,
                };

                await using var lease = await _leaseService.TryAcquireAsync($"KustoBlobIngestion-{blob.GetContainerName()}-{blob.GetBlobName()}");
                if (!lease.Acquired)
                {
                    _logger.LogWarning(
                        "Kusto blob ingestion lease for blob {ContainerName}/{BlobName} is not available.",
                        blob.GetContainerName(),
                        blob.GetBlobName());
                    message.AttemptCount++;
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount, factor: 10));
                    return;
                }

                _logger.LogInformation(
                    "Starting ingestion of blob {ContainerName}/{BlobName} into Kusto table {KustoTable}.",
                    blob.GetContainerName(),
                    blob.GetBlobName(),
                    tempTableName);
                var result = await _kustoQueuedIngestClient.IngestFromStorageAsync(
                    blobUrlWithSas.AbsoluteUri,
                    ingestionProperties,
                    sourceOptions);

                // To make the queued ingestion result queryable across method invocations, we must do some dirty hacks.
                // We need to capture the "IngestionStatusTable" property on the "TableReportIngestionResult" class
                // returned here. We capture the table URL and SAS so that we can poll the ingestion status later.
                var resultType = result.GetType();
                var tableProperty = resultType.GetProperty("IngestionStatusTable");
                var table = (CloudTable)tableProperty.GetValue(result);
                var tableSas = table.ServiceClient.Credentials.SASToken;
                var tableUrl = new UriBuilder(table.Uri) { Query = tableSas };

                blob.StatusUrl = tableUrl.Uri.AbsoluteUri;
                blob.State = KustoBlobIngestionState.Working;
                blob.Started = DateTimeOffset.UtcNow;
                await _storageService.ReplaceBlobAsync(blob);
            }

            if (blob.State == KustoBlobIngestionState.Working)
            {
                var statusList = await GetIngestionStatusListAsync(blob);
                var statusSummary = statusList
                    .GroupBy(x => x.Status)
                    .OrderBy(x => x.Key.ToString())
                    .Select(x => $"{x.Key} ({x.Count()}x)")
                    .ToList();
                _logger.LogInformation("Ingestion status: {Statuses}", statusSummary);

                var duration = DateTimeOffset.UtcNow - blob.Started.Value;

                if (duration > _options.Value.KustoBlobIngestionTimeout)
                {
                    _telemetryClient.TrackMetric(
                        nameof(KustoBlobIngestionMessageProcessor) + ".TimedOut.ElapsedMs",
                        duration.TotalMilliseconds,
                        new Dictionary<string, string>
                        {
                            { "ContainerName", blob.GetContainerName() },
                            { "BlobName", blob.GetBlobName() },
                            { "SourceId", blob.SourceId.ToString() },
                        });

                    _logger.LogWarning(
                        "The ingestion of blob {ContainerName}{BlobName} timed out after {Duration}.",
                        blob.GetContainerName(),
                        blob.GetBlobName(),
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
                            { "ContainerName", blob.GetContainerName() },
                            { "BlobName", blob.GetBlobName() },
                            { "SourceId", blob.SourceId.ToString() },
                            { "StatusSummary", string.Join(", ", statusSummary) },
                        });

                    _logger.LogWarning(
                        "The ingestion of blob {ContainerName}{BlobName} did not succeed. The statuses were: {StatusSummary}",
                        blob.GetContainerName(),
                        blob.GetBlobName(),
                        statusSummary);

                    blob.State = KustoBlobIngestionState.Failed;
                    await _storageService.ReplaceBlobAsync(blob);
                }
                else
                {
                    _telemetryClient.TrackMetric(
                        nameof(KustoBlobIngestionMessageProcessor) + ".Complete.ElapsedMs",
                        duration.TotalMilliseconds,
                        new Dictionary<string, string>());

                    _logger.LogInformation(
                        "The ingestion of blob {ContainerName}{BlobName} is complete.",
                        blob.GetContainerName(),
                        blob.GetBlobName());
                    await _storageService.DeleteBlobAsync(blob);
                }
            }
        }

        private async Task<List<IngestionStatus>> GetIngestionStatusListAsync(KustoBlobIngestion blob)
        {
            using var metrics = _telemetryClient.StartQueryLoopMetrics();

            var statusTable = new CloudTable(new Uri(blob.StatusUrl));
            _logger.LogInformation(
                "Checking ingestion status of blob {ContainerName}/{BlobName} in status table {StatusTable}.",
                blob.GetContainerName(),
                blob.GetBlobName(),
                statusTable.Uri.AbsoluteUri);
            var statusQuery = new TableQuery<IngestionStatus>
            {
                FilterString = TableQuery.GenerateFilterCondition(
                    StorageUtility.PartitionKey,
                    QueryComparisons.Equal,
                    blob.SourceId.ToString()),
                TakeCount = StorageUtility.MaxTakeCount,
            };

            TableContinuationToken token = null;
            var statusList = new List<IngestionStatus>();
            do
            {
                TableQuerySegment<IngestionStatus> segment;
                using (metrics.TrackQuery())
                {
                    segment = await statusTable.ExecuteQuerySegmentedAsync(statusQuery, token);
                }

                statusList.AddRange(segment.Results);
                token = segment.ContinuationToken;
            }
            while (token != null);
            _logger.LogInformation("Fetched {Count} ingestion status records.", statusList.Count);

            return statusList;
        }
    }
}

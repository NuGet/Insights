using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kusto.Data.Common;
using Kusto.Ingest;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.Worker.KustoIngestion
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
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;
        private readonly ILogger<KustoBlobIngestionMessageProcessor> _logger;

        public KustoBlobIngestionMessageProcessor(
            KustoIngestionStorageService storageService,
            AutoRenewingStorageLeaseService leaseService,
            CsvResultStorageContainers csvRecordContainers,
            ServiceClientFactory serviceClientFactory,
            IKustoQueuedIngestClient kustoQueuedIngestClient,
            IMessageEnqueuer messageEnqueuer,
            ITelemetryClient telemetryClient,
            IOptions<ExplorePackagesWorkerSettings> options,
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
                _logger.LogWarning("No matching Kusto container ingestion was found.");
                return;
            }

            if (blob.State == KustoBlobIngestionState.Created)
            {
                var sas = await _serviceClientFactory.GetBlobReadStorageSharedAccessSignatureAsync();
                var uriBuilder = new UriBuilder(blob.SourceUrl) { Query = sas };
                var blobUrlWithSas = uriBuilder.Uri.AbsoluteUri;

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
                    _logger.LogWarning("Kusto blob ingestion lease for {ContainerName} and blob {BlobName} is not available.", blob.GetContainerName(), blob.GetBlobName());
                    message.AttemptCount++;
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount, factor: 10));
                    return;
                }

                _logger.LogInformation("Starting ingestion of blob {SourceUrl} into Kusto table {KustoTable}.", blob.SourceUrl, tempTableName);
                var result = await _kustoQueuedIngestClient.IngestFromStorageAsync(
                    blobUrlWithSas,
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

                if (statusList.Any(x => x.Status == Status.Pending))
                {
                    message.AttemptCount++;
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount, factor: 10));
                    return;
                }
                else if (statusList.Any(x => x.Status != Status.Succeeded))
                {
                    throw new InvalidOperationException($"The ingestion did not succeed. The statuses were: {string.Join(", ", statusSummary)}");
                }
                else
                {
                    _logger.LogInformation("The ingestion of blob {SourceUrl} is complete.", blob.SourceUrl);
                    await _storageService.DeleteBlobAsync(blob);
                }
            }
        }

        private async Task<List<IngestionStatus>> GetIngestionStatusListAsync(KustoBlobIngestion blob)
        {
            using var metrics = _telemetryClient.StartQueryLoopMetrics();

            var statusTable = new CloudTable(new Uri(blob.StatusUrl));
            _logger.LogInformation(
                "Checking ingestion status of blob {SourceUrl} in status table {StatusTable}.",
                blob.SourceUrl,
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

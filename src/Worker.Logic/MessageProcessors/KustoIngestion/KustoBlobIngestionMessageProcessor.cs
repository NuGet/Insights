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
        private readonly CsvRecordContainers _csvRecordContainers;
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IKustoQueuedIngestClient _kustoQueuedIngestClient;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;
        private readonly ILogger<KustoBlobIngestionMessageProcessor> _logger;

        public KustoBlobIngestionMessageProcessor(
            KustoIngestionStorageService storageService,
            CsvRecordContainers csvRecordContainers,
            ServiceClientFactory serviceClientFactory,
            IKustoQueuedIngestClient kustoQueuedIngestClient,
            IMessageEnqueuer messageEnqueuer,
            IOptions<ExplorePackagesWorkerSettings> options,
            ILogger<KustoBlobIngestionMessageProcessor> logger)
        {
            _storageService = storageService;
            _csvRecordContainers = csvRecordContainers;
            _serviceClientFactory = serviceClientFactory;
            _kustoQueuedIngestClient = kustoQueuedIngestClient;
            _messageEnqueuer = messageEnqueuer;
            _options = options;
            _logger = logger;
        }

        public async Task ProcessAsync(KustoBlobIngestionMessage message, long dequeueCount)
        {
            var blob = await _storageService.GetBlobAsync(message.StorageSuffix, message.ContainerName, message.Bucket);
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
                if (statusList.Any(x => x.Status == Status.Pending))
                {
                    message.AttemptCount++;
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount, factor: 10));
                    return;
                }
                else if (statusList.Any(x => x.Status != Status.Succeeded))
                {
                    var statusSummary = statusList.GroupBy(x => x.Status).Select(x => $"{x.Key} ({x.Count()}x)");
                    throw new InvalidOperationException($"The ingestion did not succeed. The statuses were: {string.Join(", ", statusSummary)}");
                }
                else
                {
                    await _storageService.DeleteBlobAsync(blob);
                }
            }
        }

        private static async Task<List<IngestionStatus>> GetIngestionStatusListAsync(KustoBlobIngestion blob)
        {
            var statusTable = new CloudTable(new Uri(blob.StatusUrl));
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
                var segment = await statusTable.ExecuteQuerySegmentedAsync(statusQuery, token);
                statusList.AddRange(segment.Results);
                token = segment.ContinuationToken;
            }
            while (token != null);

            return statusList;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kusto.Data.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Worker.KustoIngestion
{
    public class KustoContainerIngestionMessageProcessor : IMessageProcessor<KustoContainerIngestionMessage>
    {
        private readonly KustoIngestionStorageService _storageService;
        private readonly CsvRecordContainers _csvRecordContainers;
        private readonly ICslAdminProvider _kustoAdminClient;
        private readonly AppendResultStorageService _appendResultStorageService;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;
        private readonly ILogger<KustoContainerIngestionMessageProcessor> _logger;

        public KustoContainerIngestionMessageProcessor(
            KustoIngestionStorageService storageService,
            CsvRecordContainers csvRecordContainers,
            ICslAdminProvider kustoAdminClient,
            AppendResultStorageService appendResultStorageService,
            IMessageEnqueuer messageEnqueuer,
            IOptions<ExplorePackagesWorkerSettings> options,
            ILogger<KustoContainerIngestionMessageProcessor> logger)
        {
            _storageService = storageService;
            _csvRecordContainers = csvRecordContainers;
            _kustoAdminClient = kustoAdminClient;
            _appendResultStorageService = appendResultStorageService;
            _messageEnqueuer = messageEnqueuer;
            _options = options;
            _logger = logger;
        }

        public async Task ProcessAsync(KustoContainerIngestionMessage message, long dequeueCount)
        {
            var container = await _storageService.GetContainerAsync(message.StorageSuffix, message.ContainerName);
            if (container == null)
            {
                _logger.LogWarning("No matching Kusto container ingestion was found.");
                return;
            }

            if (container.State == KustoContainerIngestionState.Created)
            {
                var buckets = await _appendResultStorageService.GetCompactedBucketsAsync(container.GetContainerName());
                if (buckets.Count == 0)
                {
                    await CompleteAsync(container);
                    return;
                }

                container.State = KustoContainerIngestionState.CreatingTable;
                await _storageService.ReplaceContainerAsync(container);
            }

            var tempTableName = _csvRecordContainers.GetTempKustoTableName(container.GetContainerName());
            if (container.State == KustoContainerIngestionState.CreatingTable)
            {
                foreach (var commandTemplate in GetDDL(container.GetContainerName()))
                {
                    var command = FormatCommand(tempTableName, commandTemplate);
                    await ExecuteKustoCommandAsync(command);
                }

                container.State = KustoContainerIngestionState.Expanding;
                await _storageService.ReplaceContainerAsync(container);
            }

            if (container.State == KustoContainerIngestionState.Expanding)
            {
                var bucketInfos = await _appendResultStorageService.GetCompactedBucketsAsync(container.GetContainerName());
                var bucketToEntity = new Dictionary<int, KustoBlobIngestion>();
                foreach (var bucketInfo in bucketInfos)
                {
                    var url = await _appendResultStorageService.GetCompactedBlobUrlAsync(container.GetContainerName(), bucketInfo.Bucket);
                    bucketToEntity.Add(bucketInfo.Bucket, new KustoBlobIngestion(container.GetContainerName(), bucketInfo.Bucket)
                    {
                        IngestionId = container.IngestionId,
                        StorageSuffix = container.StorageSuffix,
                        RawSizeBytes = bucketInfo.RawSizeBytes,
                        SourceId = Guid.NewGuid(),
                        SourceUrl = url.AbsoluteUri,
                        State = KustoBlobIngestionState.Created,
                    });
                }

                await _storageService.AddBlobsAsync(container, bucketToEntity.Values.ToList());

                container.State = KustoContainerIngestionState.Enqueuing;
                await _storageService.ReplaceContainerAsync(container);
            }

            if (container.State == KustoContainerIngestionState.Enqueuing)
            {
                var blobs = await _storageService.GetBlobsAsync(container);
                await _messageEnqueuer.EnqueueAsync(blobs.Select(x => new KustoBlobIngestionMessage
                {
                    StorageSuffix = x.StorageSuffix,
                    ContainerName = x.GetContainerName(),
                    Bucket = x.Bucket,
                }).ToList());

                container.State = KustoContainerIngestionState.Working;
                await _storageService.ReplaceContainerAsync(container);
            }

            if (container.State == KustoContainerIngestionState.Working)
            {
                var countLowerBound = await _storageService.GetBlobCountLowerBoundAsync(container.StorageSuffix, container.GetContainerName());
                if (countLowerBound > 0)
                {
                    _logger.LogInformation(
                        "There are at least {CountLowerBound} blobs in container {ContainerName} still being ingested into Kusto.",
                        container.GetContainerName(),
                        countLowerBound);
                    message.AttemptCount++;
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount));
                    return;
                }
                else
                {
                    container.State = KustoContainerIngestionState.SwappingTable;
                    await _storageService.ReplaceContainerAsync(container);
                }
            }

            var finalTableName = _csvRecordContainers.GetKustoTableName(container.GetContainerName());
            var oldTableName = finalTableName + "_Old";
            if (container.State == KustoContainerIngestionState.SwappingTable)
            {
                await ExecuteKustoCommandAsync($".drop table {oldTableName} ifexists");
                await ExecuteKustoCommandAsync($".rename tables {oldTableName}={finalTableName} ifexists, {finalTableName}={tempTableName}");

                container.State = KustoContainerIngestionState.DroppingOldTable;
                await _storageService.ReplaceContainerAsync(container);
            }

            if (container.State == KustoContainerIngestionState.DroppingOldTable)
            {
                await ExecuteKustoCommandAsync($".drop table {oldTableName} ifexists");

                await CompleteAsync(container);
            }
        }

        private async Task CompleteAsync(KustoContainerIngestion container)
        {
            await _storageService.DeleteContainerAsync(container);
        }

        private async Task ExecuteKustoCommandAsync(string command)
        {
            using (await _kustoAdminClient.ExecuteControlCommandAsync(_options.Value.KustoDatabaseName, command))
            {
            }
        }

        private IReadOnlyList<string> GetDDL(string containerName)
        {
            var recordType = _csvRecordContainers.GetRecordType(containerName);
            return KustoDDL.TypeToDDL[recordType];
        }

        private string FormatCommand(string tableName, string commandTemplate)
        {
            return commandTemplate.Replace("__TABLENAME__", tableName);
        }
    }
}

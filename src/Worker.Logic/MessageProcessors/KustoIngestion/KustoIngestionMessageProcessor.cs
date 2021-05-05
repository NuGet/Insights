using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Worker.KustoIngestion
{
    public class KustoIngestionMessageProcessor : IMessageProcessor<KustoIngestionMessage>
    {
        private readonly KustoIngestionStorageService _storageService;
        private readonly CsvRecordContainers _csvRecordContainers;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly ILogger<KustoIngestionMessageProcessor> _logger;

        public KustoIngestionMessageProcessor(
            KustoIngestionStorageService storageService,
            CsvRecordContainers csvRecordContainers,
            IMessageEnqueuer messageEnqueuer,
            ILogger<KustoIngestionMessageProcessor> logger)
        {
            _storageService = storageService;
            _csvRecordContainers = csvRecordContainers;
            _messageEnqueuer = messageEnqueuer;
            _logger = logger;
        }

        public async Task ProcessAsync(KustoIngestionMessage message, long dequeueCount)
        {
            var ingestion = await _storageService.GetIngestionAsync(message.IngestionId);
            if (ingestion == null)
            {
                await Task.Delay(TimeSpan.FromSeconds(dequeueCount * 15));
                throw new InvalidOperationException($"An incomplete Kusto ingestion should have already been created.");
            }

            if (ingestion.State == KustoIngestionState.Created)
            {
                ingestion.Created = DateTimeOffset.UtcNow;
                ingestion.State = KustoIngestionState.Expanding;
                await _storageService.ReplaceIngestionAsync(ingestion);
            }

            if (ingestion.State == KustoIngestionState.Expanding)
            {
                await _storageService.InitializeChildTableAsync(ingestion.StorageSuffix);

                var allContainerNames = _csvRecordContainers.GetContainerNames();
                await _storageService.AddContainersAsync(ingestion, allContainerNames);

                ingestion.State = KustoIngestionState.Enqueuing;
                await _storageService.ReplaceIngestionAsync(ingestion);
            }

            if (ingestion.State == KustoIngestionState.Enqueuing)
            {
                var containers = await _storageService.GetContainersAsync(ingestion);
                await _messageEnqueuer.EnqueueAsync(containers.Select(x => new KustoContainerIngestionMessage
                {
                    StorageSuffix = x.StorageSuffix,
                    ContainerName = x.GetContainerName(),
                }).ToList());

                ingestion.State = KustoIngestionState.Working;
                await _storageService.ReplaceIngestionAsync(ingestion);
            }

            if (ingestion.State == KustoIngestionState.Working)
            {
                var countLowerBound = await _storageService.GetContainerCountLowerBoundAsync(ingestion.StorageSuffix);
                if (countLowerBound > 0)
                {
                    _logger.LogInformation("There are at least {CountLowerBound} containers still being ingested into Kusto.", countLowerBound);
                    message.AttemptCount++;
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount));
                    return;
                }
                else
                {
                    ingestion.State = KustoIngestionState.Finalizing;
                    await _storageService.ReplaceIngestionAsync(ingestion);
                }
            }

            if (ingestion.State == KustoIngestionState.Finalizing)
            {
                await _storageService.DeleteChildTableAsync(ingestion.StorageSuffix);

                ingestion.Completed = DateTimeOffset.UtcNow;
                ingestion.State = KustoIngestionState.Complete;
                await _storageService.ReplaceIngestionAsync(ingestion);
            }
        }
    }
}

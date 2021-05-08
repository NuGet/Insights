using System;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker.KustoIngestion
{
    public class KustoIngestionService
    {
        private readonly KustoIngestionStorageService _storageService;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly AutoRenewingStorageLeaseService _leaseService;

        public KustoIngestionService(
            KustoIngestionStorageService storageService,
            IMessageEnqueuer messageEnqueuer,
            AutoRenewingStorageLeaseService leaseService)
        {
            _storageService = storageService;
            _messageEnqueuer = messageEnqueuer;
            _leaseService = leaseService;
        }

        public async Task InitializeAsync()
        {
            await _storageService.InitializeAsync();
            await _messageEnqueuer.InitializeAsync();
            await _leaseService.InitializeAsync();
        }

        public async Task ExecuteIfNoIngestionIsRunningAsync(Func<Task> actionAsync)
        {
            await using (var lease = await GetStartLeaseAsync())
            {
                if (!lease.Acquired)
                {
                    return;
                }

                if (await _storageService.IsIngestionRunningAsync())
                {
                    return;
                }

                await actionAsync();
            }
        }

        public async Task<KustoIngestionEntity> StartAsync()
        {
            await using (var lease = await GetStartLeaseAsync())
            {
                if (!lease.Acquired)
                {
                    return null;
                }

                if (await _storageService.IsIngestionRunningAsync())
                {
                    return null;
                }

                var storageId = StorageUtility.GenerateDescendingId();
                var ingestion = new KustoIngestionEntity(storageId.ToString(), storageId.Unique);

                await _messageEnqueuer.EnqueueAsync(new[]
                {
                    new KustoIngestionMessage
                    {
                        IngestionId = ingestion.GetIngestionId(),
                    },
                });

                await _storageService.AddIngestionAsync(ingestion);
                return ingestion;
            }
        }

        private async Task<AutoRenewingStorageLeaseResult> GetStartLeaseAsync()
        {
            return await _leaseService.TryAcquireAsync("Start-KustoIngestion");
        }
    }
}

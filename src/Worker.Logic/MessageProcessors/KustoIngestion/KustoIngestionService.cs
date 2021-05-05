using System.Linq;
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

        public async Task StartAsync()
        {
            await using (var lease = await _leaseService.TryAcquireAsync("Start-KustoIngestion"))
            {
                if (!lease.Acquired)
                {
                    return;
                }

                var latestIngestions = await _storageService.GetLatestIngestionsAsync();
                if (latestIngestions.Any(x => x.State != KustoIngestionState.Complete))
                {
                    return;
                }

                var storageId = StorageUtility.GenerateDescendingId();
                var ingestion = new KustoIngestion(storageId.ToString(), storageId.Unique);

                await _messageEnqueuer.EnqueueAsync(new[]
                {
                    new KustoIngestionMessage
                    {
                        IngestionId = ingestion.GetIngestionId(),
                    },
                });

                await _storageService.AddIngestionAsync(ingestion);
            }
        }
    }
}

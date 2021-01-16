using Microsoft.Extensions.Options;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker.DownloadsToCsv
{
    public class DownloadsToCsvService
    {
        private static readonly string StorageSuffix = string.Empty;
        private static readonly string PartitionKey = "DownloadsToCsv";

        private readonly MessageEnqueuer _messageEnqueuer;
        private readonly TaskStateStorageService _taskStateStorageService;
        private readonly AppendResultStorageService _resultStorageService;
        private readonly AutoRenewingStorageLeaseService _leaseService;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;

        public DownloadsToCsvService(
            MessageEnqueuer messageEnqueuer,
            TaskStateStorageService taskStateStorageService,
            AppendResultStorageService resultStorageService,
            AutoRenewingStorageLeaseService leaseService,
            IOptions<ExplorePackagesWorkerSettings> options)
        {
            _messageEnqueuer = messageEnqueuer;
            _taskStateStorageService = taskStateStorageService;
            _resultStorageService = resultStorageService;
            _leaseService = leaseService;
            _options = options;
        }

        public async Task InitializeAsync()
        {
            await _leaseService.InitializeAsync();
            await _messageEnqueuer.InitializeAsync();
            await _taskStateStorageService.InitializeAsync(StorageSuffix);
            await _resultStorageService.InitializeAsync(
                _options.Value.PackageDownloadsAppendTableName,
                _options.Value.PackageDownloadsContainerName);
        }

        public async Task StartAsync()
        {
            if (await IsRunningAsync())
            {
                return;
            }

            await using (var lease = await _leaseService.TryAcquireAsync("Start-DownloadsToCsv"))
            {
                if (!lease.Acquired)
                {
                    return;
                }

                if (await IsRunningAsync())
                {
                    return;
                }

                var taskStateKey = new TaskStateKey(
                    StorageSuffix,
                    PartitionKey,
                    StorageUtility.GenerateDescendingId().ToString());
                await _messageEnqueuer.EnqueueAsync(new[] { new DownloadsToCsvMessage { TaskStateKey = taskStateKey } });
                await _taskStateStorageService.GetOrAddAsync(taskStateKey);
            }
        }

        public async Task<bool> IsRunningAsync()
        {
            var countLowerBound = await _taskStateStorageService.GetCountLowerBoundAsync(StorageSuffix, PartitionKey);
            return countLowerBound > 0;
        }
    }
}

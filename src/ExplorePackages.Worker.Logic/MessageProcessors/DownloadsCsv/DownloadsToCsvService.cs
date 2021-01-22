using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Worker.DownloadsToCsv
{
    public class DownloadsToCsvService
    {
        private static readonly string StorageSuffix = string.Empty;
        private static readonly string PartitionKey = "DownloadsToCsv";

        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly TaskStateStorageService _taskStateStorageService;
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly AutoRenewingStorageLeaseService _leaseService;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;

        public DownloadsToCsvService(
            IMessageEnqueuer messageEnqueuer,
            TaskStateStorageService taskStateStorageService,
            AutoRenewingStorageLeaseService leaseService,
            ServiceClientFactory serviceClientFactory,
            IOptions<ExplorePackagesWorkerSettings> options)
        {
            _messageEnqueuer = messageEnqueuer;
            _taskStateStorageService = taskStateStorageService;
            _serviceClientFactory = serviceClientFactory;
            _leaseService = leaseService;
            _options = options;
        }

        public async Task InitializeAsync()
        {
            await _leaseService.InitializeAsync();
            await _messageEnqueuer.InitializeAsync();
            await _taskStateStorageService.InitializeAsync(StorageSuffix);
            await _serviceClientFactory
                .GetStorageAccount()
                .CreateCloudBlobClient()
                .GetContainerReference(_options.Value.PackageDownloadsContainerName)
                .CreateIfNotExistsAsync(retry: true);
        }

        public async Task StartAsync(bool loop, TimeSpan notBefore)
        {
            await using (var lease = await _leaseService.TryAcquireAsync("Start-DownloadsToCsv"))
            {
                if (!lease.Acquired)
                {
                    throw new InvalidOperationException("Another actor is already starting DownloadsToCsv.");
                }

                var taskStateKey = new TaskStateKey(
                    StorageSuffix,
                    PartitionKey,
                    StorageUtility.GenerateDescendingId().ToString());
                await _messageEnqueuer.EnqueueAsync(new[] { new DownloadsToCsvMessage { TaskStateKey = taskStateKey, Loop = loop } }, notBefore);
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

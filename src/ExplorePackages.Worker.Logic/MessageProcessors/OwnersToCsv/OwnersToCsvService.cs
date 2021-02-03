using System;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker.OwnersToCsv
{
    public class OwnersToCsvService
    {
        private static readonly string StorageSuffix = string.Empty;
        private static readonly string PartitionKey = "OwnersToCsv";

        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly TaskStateStorageService _taskStateStorageService;
        private readonly AutoRenewingStorageLeaseService _leaseService;

        public OwnersToCsvService(
            IMessageEnqueuer messageEnqueuer,
            TaskStateStorageService taskStateStorageService,
            AutoRenewingStorageLeaseService leaseService)
        {
            _messageEnqueuer = messageEnqueuer;
            _taskStateStorageService = taskStateStorageService;
            _leaseService = leaseService;
        }

        public async Task InitializeAsync()
        {
            await _leaseService.InitializeAsync();
            await _messageEnqueuer.InitializeAsync();
            await _taskStateStorageService.InitializeAsync(StorageSuffix);
        }

        public async Task StartAsync(bool loop, TimeSpan notBefore)
        {
            await using (var lease = await _leaseService.TryAcquireAsync("Start-OwnersToCsv"))
            {
                if (!lease.Acquired)
                {
                    throw new InvalidOperationException("Another actor is already starting OwnersToCsv.");
                }

                var taskStateKey = new TaskStateKey(
                    StorageSuffix,
                    PartitionKey,
                    StorageUtility.GenerateDescendingId().ToString());
                await _messageEnqueuer.EnqueueAsync(new[] { new OwnersToCsvMessage { TaskStateKey = taskStateKey, Loop = loop } }, notBefore);
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

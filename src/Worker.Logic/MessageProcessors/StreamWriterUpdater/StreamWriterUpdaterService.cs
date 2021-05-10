using System;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker.StreamWriterUpdater
{
    public class StreamWriterUpdaterService<T> : IStreamWriterUpdaterService<T> where T : IAsyncDisposable, IAsOfData
    {
        private static readonly string StorageSuffix = string.Empty;

        private readonly IStreamWriterUpdater<T> _updater;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly TaskStateStorageService _taskStateStorageService;
        private readonly AutoRenewingStorageLeaseService _leaseService;

        public StreamWriterUpdaterService(
            IStreamWriterUpdater<T> updater,
            IMessageEnqueuer messageEnqueuer,
            TaskStateStorageService taskStateStorageService,
            AutoRenewingStorageLeaseService leaseService)
        {
            _updater = updater;
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

        public bool IsEnabled => _updater.IsEnabled;

        public async Task<bool> StartAsync()
        {
            await using (var lease = await _leaseService.TryAcquireAsync($"Start-{_updater.OperationName}"))
            {
                if (!lease.Acquired)
                {
                    return false;
                }

                var taskStateKey = new TaskStateKey(
                    StorageSuffix,
                    _updater.OperationName,
                    StorageUtility.GenerateDescendingId().ToString());
                await _messageEnqueuer.EnqueueAsync(new[] { new StreamWriterUpdaterMessage<T> { TaskStateKey = taskStateKey } });
                await _taskStateStorageService.AddAsync(taskStateKey);
                return true;
            }
        }

        public async Task<bool> IsRunningAsync()
        {
            var countLowerBound = await _taskStateStorageService.GetCountLowerBoundAsync(StorageSuffix, _updater.OperationName);
            return countLowerBound > 0;
        }
    }
}

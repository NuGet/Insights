// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.AuxiliaryFileUpdater
{
    public class AuxiliaryFileUpdaterService<TInput, TRecord> : IAuxiliaryFileUpdaterService<TRecord>
        where TInput : IAsOfData
        where TRecord : IAuxiliaryFileCsvRecord<TRecord>
    {
        private readonly ContainerInitializationState _initializationState;
        private readonly ContainerInitializationState _containerState;
        private readonly IAuxiliaryFileUpdater<TInput, TRecord> _updater;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly TaskStateStorageService _taskStateStorageService;
        private readonly AutoRenewingStorageLeaseService _leaseService;

        public AuxiliaryFileUpdaterService(
            IAuxiliaryFileUpdater<TInput, TRecord> updater,
            IMessageEnqueuer messageEnqueuer,
            TaskStateStorageService taskStateStorageService,
            AutoRenewingStorageLeaseService leaseService,
            ServiceClientFactory serviceClientFactory)
        {
            _initializationState = ContainerInitializationState.New(InitializeInternalAsync, DestroyInternalAsync);
            _containerState = ContainerInitializationState.BlobContainer(serviceClientFactory, updater.ContainerName);
            _updater = updater;
            _messageEnqueuer = messageEnqueuer;
            _taskStateStorageService = taskStateStorageService;
            _leaseService = leaseService;
        }

        public async Task InitializeAsync()
        {
            await _initializationState.InitializeAsync();
        }

        public async Task DestroyAsync()
        {
            await Task.WhenAll(
                _initializationState.DestroyAsync(),
                _containerState.DestroyAsync());
        }

        private async Task InitializeInternalAsync()
        {
            await Task.WhenAll(
                _leaseService.InitializeAsync(),
                _messageEnqueuer.InitializeAsync(),
                _taskStateStorageService.InitializeAsync(TaskStateStorageService.SingletonStorageSuffix));
        }

        private async Task DestroyInternalAsync()
        {
            var taskStates = await _taskStateStorageService.GetByRowKeyPrefixAsync(TaskStateStorageService.SingletonStorageSuffix, _updater.OperationName, string.Empty);
            await _taskStateStorageService.DeleteAsync(taskStates);
        }

        public async Task<bool> StartAsync()
        {
            await using (var lease = await _leaseService.TryAcquireWithRetryAsync($"Start-AuxiliaryFileUpdater-{_updater.OperationName}"))
            {
                if (!lease.Acquired)
                {
                    return false;
                }

                await InitializeAsync();

                if (await IsRunningAsync())
                {
                    return false;
                }

                await _containerState.InitializeAsync();

                var taskStateKey = new TaskStateKey(
                    TaskStateStorageService.SingletonStorageSuffix,
                    _updater.OperationName,
                    StorageUtility.GenerateDescendingId().ToString());
                await _messageEnqueuer.EnqueueAsync([new AuxiliaryFileUpdaterMessage<TRecord> { TaskStateKey = taskStateKey }]);
                await _taskStateStorageService.GetOrAddAsync(taskStateKey);
                return true;
            }
        }

        public async Task<bool> IsRunningAsync()
        {
            var countLowerBound = await _taskStateStorageService.GetCountLowerBoundAsync(TaskStateStorageService.SingletonStorageSuffix, _updater.OperationName);
            return countLowerBound > 0;
        }
    }
}

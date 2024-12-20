// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Storage.Blobs;

namespace NuGet.Insights.Worker.AuxiliaryFileUpdater
{
    public class AuxiliaryFileUpdaterService<TInput, TRecord> : IAuxiliaryFileUpdaterService<TRecord>
        where TInput : IAsOfData
        where TRecord : IAuxiliaryFileCsvRecord<TRecord>
    {
        private static readonly string StorageSuffix = string.Empty;

        private readonly IAuxiliaryFileUpdater<TInput, TRecord> _updater;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly TaskStateStorageService _taskStateStorageService;
        private readonly AutoRenewingStorageLeaseService _leaseService;
        private readonly ServiceClientFactory _serviceClientFactory;

        public AuxiliaryFileUpdaterService(
            IAuxiliaryFileUpdater<TInput, TRecord> updater,
            IMessageEnqueuer messageEnqueuer,
            TaskStateStorageService taskStateStorageService,
            AutoRenewingStorageLeaseService leaseService,
            ServiceClientFactory serviceClientFactory)
        {
            _updater = updater;
            _messageEnqueuer = messageEnqueuer;
            _taskStateStorageService = taskStateStorageService;
            _leaseService = leaseService;
            _serviceClientFactory = serviceClientFactory;
        }

        public async Task InitializeAsync()
        {
            await _leaseService.InitializeAsync();
            await _messageEnqueuer.InitializeAsync();
            await _taskStateStorageService.InitializeAsync(TaskStateStorageService.SingletonStorageSuffix);
            await CreateContainerAsync();
        }

        private async Task CreateContainerAsync()
        {
            await (await GetContainerAsync()).CreateIfNotExistsAsync(retry: true);
        }

        public async Task DestroyAsync()
        {
            await (await GetContainerAsync()).DeleteIfExistsAsync();

            var taskStates = await _taskStateStorageService.GetByRowKeyPrefixAsync(TaskStateStorageService.SingletonStorageSuffix, _updater.OperationName, string.Empty);
            await _taskStateStorageService.DeleteAsync(taskStates);
        }

        private async Task<BlobContainerClient> GetContainerAsync()
        {
            var serviceClient = await _serviceClientFactory.GetBlobServiceClientAsync();
            var container = serviceClient.GetBlobContainerClient(_updater.ContainerName);
            return container;
        }

        public async Task<bool> StartAsync()
        {
            await using (var lease = await _leaseService.TryAcquireWithRetryAsync($"Start-AuxiliaryFileUpdater-{_updater.OperationName}"))
            {
                if (!lease.Acquired)
                {
                    return false;
                }

                await CreateContainerAsync();

                if (await IsRunningAsync())
                {
                    return false;
                }

                var taskStateKey = new TaskStateKey(
                    StorageSuffix,
                    _updater.OperationName,
                    StorageUtility.GenerateDescendingId().ToString());
                await _messageEnqueuer.EnqueueAsync(new[] { new AuxiliaryFileUpdaterMessage<TRecord> { TaskStateKey = taskStateKey } });
                await _taskStateStorageService.GetOrAddAsync(taskStateKey);
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

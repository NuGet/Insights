// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.ReferenceTracking;

namespace NuGet.Insights.Worker.ReferenceTracking
{
    public class CleanupOrphanRecordsService<T> : ICleanupOrphanRecordsService<T> where T : ICleanupOrphanCsvRecord
    {
        private readonly ContainerInitializationState _initializationState;
        private readonly ContainerInitializationState _referenceTrackerInitializationState;
        private readonly AutoRenewingStorageLeaseService _leaseService;
        private readonly ICleanupOrphanRecordsAdapter<T> _adapter;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly TaskStateStorageService _taskStateStorageService;
        private readonly ReferenceTracker _referenceTracker;
        private readonly SchemaSerializer _schemaSerializer;

        public CleanupOrphanRecordsService(
            AutoRenewingStorageLeaseService leaseService,
            ICleanupOrphanRecordsAdapter<T> adapter,
            IMessageEnqueuer messageEnqueuer,
            TaskStateStorageService taskStateStorageService,
            ReferenceTracker referenceTracker,
            SchemaSerializer schemaSerializer)
        {
            _initializationState = ContainerInitializationState.New(InitializeInternalAsync);
            _referenceTrackerInitializationState = ContainerInitializationState.New(() => referenceTracker.InitializeAsync(
                adapter.OwnerToSubjectTableName,
                adapter.SubjectToOwnerTableName));
            _leaseService = leaseService;
            _adapter = adapter;
            _messageEnqueuer = messageEnqueuer;
            _taskStateStorageService = taskStateStorageService;
            _referenceTracker = referenceTracker;
            _schemaSerializer = schemaSerializer;
        }

        public async Task InitializeAsync()
        {
            await _initializationState.InitializeAsync();
        }

        private async Task InitializeInternalAsync()
        {
            await Task.WhenAll(
                _leaseService.InitializeAsync(),
                _messageEnqueuer.InitializeAsync(),
                _taskStateStorageService.InitializeAsync(TaskStateStorageService.SingletonStorageSuffix));
        }

        private string OperationName => $"CleanupOrphans-{_adapter.OwnerType}-{_adapter.SubjectType}";

        public async Task<bool> StartAsync()
        {
            await using (var lease = await _leaseService.TryAcquireWithRetryAsync(OperationName))
            {
                if (!lease.Acquired)
                {
                    return false;
                }

                if (await IsRunningAsync())
                {
                    return false;
                }

                await _referenceTrackerInitializationState.InitializeAsync();

                var cleanupId = StorageUtility.GenerateDescendingId();
                var cleanupIdStr = cleanupId.ToString();
                var storageSuffix = cleanupId.Unique;
                var parameters = _schemaSerializer.Serialize(new CleanupOrphanRecordsParameters()).AsString();
                var taskState = new TaskState(TaskStateStorageService.SingletonStorageSuffix, OperationName, cleanupIdStr)
                {
                    Parameters = parameters,
                };

                await _messageEnqueuer.EnqueueAsync(new[]
                {
                    new CleanupOrphanRecordsMessage<T>
                    {
                        TaskStateKey = taskState.GetKey(),
                        CleanupId = cleanupIdStr,
                        StorageSuffix = storageSuffix,
                    }
                });

                await _taskStateStorageService.GetOrAddAsync(taskState);

                return true;
            }
        }

        public async Task<bool> IsRunningAsync()
        {
            var countLowerBound = await _taskStateStorageService.GetCountLowerBoundAsync(TaskStateStorageService.SingletonStorageSuffix, OperationName);
            return countLowerBound > 0;
        }
    }
}

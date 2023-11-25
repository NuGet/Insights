// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.ReferenceTracking;

namespace NuGet.Insights.Worker.ReferenceTracking
{
    public class CleanupOrphanRecordsService<T> : ICleanupOrphanRecordsService<T> where T : ICsvRecord
    {
        private static readonly string StorageSuffix = string.Empty;

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
            _leaseService = leaseService;
            _adapter = adapter;
            _messageEnqueuer = messageEnqueuer;
            _taskStateStorageService = taskStateStorageService;
            _referenceTracker = referenceTracker;
            _schemaSerializer = schemaSerializer;
        }

        public async Task InitializeAsync()
        {
            await _leaseService.InitializeAsync();
            await _messageEnqueuer.InitializeAsync();
            await _taskStateStorageService.InitializeAsync(StorageSuffix);
            await _referenceTracker.InitializeAsync(
                _adapter.OwnerToSubjectTableName,
                _adapter.SubjectToOwnerTableName);
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

                var cleanupId = StorageUtility.GenerateDescendingId();
                var cleanupIdStr = cleanupId.ToString();
                var storageSuffix = cleanupId.Unique;
                var parameters = _schemaSerializer.Serialize(new CleanupOrphanRecordsParameters()).AsString();
                var taskState = new TaskState(StorageSuffix, OperationName, cleanupIdStr)
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

                await _taskStateStorageService.AddAsync(taskState);

                return true;
            }
        }

        public async Task<bool> IsRunningAsync()
        {
            var countLowerBound = await _taskStateStorageService.GetCountLowerBoundAsync(StorageSuffix, OperationName);
            return countLowerBound > 0;
        }
    }
}

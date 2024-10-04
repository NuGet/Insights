// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using Azure.Data.Tables;
using NuGet.Insights.StorageNoOpRetry;

#nullable enable

namespace NuGet.Insights.Worker
{
    public class TaskStateStorageService
    {
        public const string SingletonStorageSuffix = "";

        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly EntityUpsertStorageService<TaskState, TaskState> _upsertStorageService;
        private readonly ITelemetryClient _telemetryClient;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public TaskStateStorageService(
            ServiceClientFactory serviceClientFactory,
            EntityUpsertStorageService<TaskState, TaskState> upsertStorageService,
            ITelemetryClient telemetryClient,
            IOptions<NuGetInsightsWorkerSettings> options)
        {
            _serviceClientFactory = serviceClientFactory;
            _upsertStorageService = upsertStorageService;
            _telemetryClient = telemetryClient;
            _options = options;
        }

        public async Task InitializeAsync(string storageSuffix)
        {
            await (await GetTableAsync(storageSuffix)).CreateIfNotExistsAsync(retry: true);
        }

        public async Task DeleteTableAsync(string storageSuffix)
        {
            await (await GetTableAsync(storageSuffix)).DeleteAsync();
        }

        public async Task<TaskState> GetOrAddAsync(TaskStateKey taskStateKey)
        {
            return (await GetOrAddAsync(taskStateKey.StorageSuffix, taskStateKey.PartitionKey, [taskStateKey.RowKey])).Single();
        }

        public async Task<TaskState> GetOrAddAsync(TaskState taskState)
        {
            return (await GetOrAddAsync(taskState.StorageSuffix, taskState.PartitionKey, [taskState])).Single();
        }

        public async Task<List<TaskState>> GetOrAddAsync(string storageSuffix, string partitionKey, IReadOnlyList<string> rowKeys)
        {
            return await GetOrAddAsync(
                storageSuffix,
                partitionKey,
                rowKeys.Select(r => new TaskState(storageSuffix, partitionKey, r)).ToList());
        }

        public async Task<bool> SetStartedAsync(TaskStateKey taskStateKey)
        {
            var table = await GetTableAsync(taskStateKey.StorageSuffix);
            var merge = new TaskState(taskStateKey.StorageSuffix, taskStateKey.PartitionKey, taskStateKey.RowKey);
            merge.Started = DateTimeOffset.UtcNow;

            try
            {
                await table.UpdateEntityAsync(merge, ETag.All, TableUpdateMode.Merge);
                return true;
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        public async Task SetMessageAsync(TaskStateKey taskStateKey, string message)
        {
            var table = await GetTableAsync(taskStateKey.StorageSuffix);
            var merge = new TaskState(taskStateKey.StorageSuffix, taskStateKey.PartitionKey, taskStateKey.RowKey);
            merge.Message = message;
            await table.UpdateEntityAsync(merge, ETag.All, TableUpdateMode.Merge);
        }

        public async Task UpdateAsync(TaskState taskState)
        {
            var table = await GetTableAsync(taskState.StorageSuffix);
            var response = await table.UpdateEntityAsync(taskState, taskState.ETag, TableUpdateMode.Replace);
            taskState.UpdateETag(response);
        }

        public async Task<List<TaskState>> GetOrAddAsync(string storageSuffix, string partitionKey, IReadOnlyList<TaskState> taskStates)
        {
            if (taskStates.Any(x => x.StorageSuffix != storageSuffix || x.PartitionKey != partitionKey))
            {
                throw new ArgumentException("All task states must have the same provided storage suffix and partition key.");
            }

            var entities = new List<TaskState>();
            foreach (var group in taskStates.GroupBy(x => x.StorageSuffix))
            {
                var table = await GetTableAsync(group.Key);
                var adapter = new UpsertAdapter(table);
                entities.AddRange(await _upsertStorageService.AddAsync(group.ToList(), adapter));
            }

            return entities;
        }

        private class UpsertAdapter : IEntityUpsertStorage<TaskState, TaskState>
        {
            public UpsertAdapter(TableClientWithRetryContext table)
            {
                Table = table;
            }

            public IReadOnlyList<string>? Select => null;
            public EntityUpsertStrategy Strategy => EntityUpsertStrategy.AddOptimistically;
            public TableClientWithRetryContext Table { get; }
            public ItemWithEntityKey<TaskState> GetItemFromRowKeyGroup(IGrouping<string, ItemWithEntityKey<TaskState>> group) => group.First();
            public (string PartitionKey, string RowKey) GetKey(TaskState item) => (item.PartitionKey, item.RowKey);
            public Task<TaskState> MapAsync(string partitionKey, string rowKey, TaskState item) => Task.FromResult(item);
            public bool ShouldReplace(TaskState item, TaskState entity) => false;
        }

        public async Task<List<TaskState>> GetAllAsync(string storageSuffix, string partitionKey)
        {
            return await (await GetTableAsync(storageSuffix))
                .QueryAsync<TaskState>(x => x.PartitionKey == partitionKey)
                .ToListAsync(_telemetryClient.StartQueryLoopMetrics());
        }

        public async Task<IReadOnlyList<TaskState>> GetUnstartedAsync(string storageSuffix, string partitionKey, int take)
        {
            return await (await GetTableAsync(storageSuffix))
                .QueryAsync<TaskState>(x => x.PartitionKey == partitionKey)
                .Where(x => x.Message is not null && !x.Started.HasValue)
                .Take(take)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<TaskState>> GetByRowKeyPrefixAsync(string storageSuffix, string partitionKey, string rowKeyPrefix)
        {
            return await (await GetTableAsync(storageSuffix))
                .QueryAsync<TaskState>(
                    x => x.PartitionKey == partitionKey
                      && string.Compare(x.RowKey, rowKeyPrefix, StringComparison.Ordinal) >= 0
                      && string.Compare(x.RowKey, rowKeyPrefix + char.MaxValue, StringComparison.Ordinal) < 0)
                .ToListAsync(_telemetryClient.StartQueryLoopMetrics());
        }

        public async Task<int> GetCountLowerBoundAsync(string storageSuffix, string partitionKey)
        {
            return await (await GetTableAsync(storageSuffix))
                .GetEntityCountLowerBoundAsync(partitionKey, _telemetryClient.StartQueryLoopMetrics());
        }

        public async Task<TaskState?> GetAsync(TaskStateKey key)
        {
            var table = await GetTableAsync(key.StorageSuffix);
            return await table.GetEntityOrNullAsync<TaskState>(key.PartitionKey, key.RowKey);
        }

        public async Task<bool> DeleteAsync(TaskState taskState)
        {
            var table = await GetTableAsync(taskState.StorageSuffix);
            var response = await table.DeleteEntityAsync(taskState.PartitionKey, taskState.RowKey);

            return response.Status switch
            {
                (int)HttpStatusCode.NoContent => true,
                (int)HttpStatusCode.NotFound => false,
                _ => throw new InvalidOperationException($"Unexpected HTTP status for task state deletion: HTTP {response.Status}."),
            };
        }

        public async Task DeleteAsync(IReadOnlyList<TaskState> taskStates)
        {
            if (taskStates.Count == 0)
            {
                return;
            }

            var keys = taskStates
                .Select(x => new { x.StorageSuffix, x.PartitionKey })
                .Distinct()
                .ToList();

            if (keys.Count != 1)
            {
                throw new ArgumentException("All task states must have the same storage suffix and partition key.");
            }

            var table = await GetTableAsync(keys[0].StorageSuffix);
            var batch = new MutableTableTransactionalBatch(table);

            foreach (var taskState in taskStates)
            {
                batch.DeleteEntity(taskState.PartitionKey, taskState.RowKey, taskState.ETag);
                if (batch.Count > StorageUtility.MaxBatchSize)
                {
                    await batch.SubmitBatchAsync();
                    batch = new MutableTableTransactionalBatch(table);
                }
            }

            await batch.SubmitBatchIfNotEmptyAsync();
        }

        private async Task<TableClientWithRetryContext> GetTableAsync(string suffix)
        {
            if (suffix is null)
            {
                throw new ArgumentNullException(nameof(suffix));
            }

            string tableName;
            if (suffix == SingletonStorageSuffix)
            {
                tableName = _options.Value.SingletonTaskStateTableName;
            }
            else
            {
                tableName = $"{_options.Value.TaskStateTableNamePrefix}{suffix}";
            }

            return (await _serviceClientFactory.GetTableServiceClientAsync()).GetTableClient(tableName);
        }
    }
}

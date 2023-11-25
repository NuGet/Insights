// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Data.Tables;
using NuGet.Insights.StorageNoOpRetry;

namespace NuGet.Insights.Worker
{
    public class TaskStateStorageService
    {
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly ITelemetryClient _telemetryClient;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public TaskStateStorageService(
            ServiceClientFactory serviceClientFactory,
            ITelemetryClient telemetryClient,
            IOptions<NuGetInsightsWorkerSettings> options)
        {
            _serviceClientFactory = serviceClientFactory;
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

        public async Task AddAsync(TaskStateKey taskStateKey)
        {
            await AddAsync(taskStateKey.StorageSuffix, taskStateKey.PartitionKey, new[] { taskStateKey.RowKey });
        }

        public async Task AddAsync(TaskState taskState)
        {
            await AddAsync(taskState.StorageSuffix, taskState.PartitionKey, new[] { taskState });
        }

        public async Task AddAsync(string storageSuffix, string partitionKey, IReadOnlyList<string> rowKeys)
        {
            await AddAsync(
                storageSuffix,
                partitionKey,
                rowKeys.Select(r => new TaskState(storageSuffix, partitionKey, r)).ToList());
        }

        public async Task UpdateAsync(TaskState taskState)
        {
            var table = await GetTableAsync(taskState.StorageSuffix);
            var response = await table.UpdateEntityAsync(taskState, taskState.ETag, TableUpdateMode.Replace);
            taskState.UpdateETag(response);
        }

        public async Task AddAsync(string storageSuffix, string partitionKey, IReadOnlyList<TaskState> taskStates)
        {
            if (taskStates.Any(x => x.StorageSuffix != storageSuffix || x.PartitionKey != partitionKey))
            {
                throw new ArgumentException("All task states must have the same provided storage suffix and partition key.");
            }

            var existing = await GetAllAsync(storageSuffix, partitionKey);
            var existingRowKeys = existing.Select(x => x.RowKey).ToHashSet();

            // Remove row keys we don't care about at all.
            existingRowKeys.IntersectWith(taskStates.Select(x => x.RowKey));

            var toInsert = taskStates
                .Where(x => !existingRowKeys.Contains(x.RowKey))
                .ToList();
            await InsertAsync(toInsert);
        }

        private async Task<IReadOnlyList<TaskState>> GetAllAsync(string storageSuffix, string partitionKey)
        {
            return await (await GetTableAsync(storageSuffix))
                .QueryAsync<TaskState>(x => x.PartitionKey == partitionKey)
                .ToListAsync(_telemetryClient.StartQueryLoopMetrics());
        }

        public async Task<IReadOnlyList<TaskState>> GetByRowKeyPrefixAsync(string storageSuffix, string partitionKey, string rowKeyPrefix)
        {
            return await (await GetTableAsync(storageSuffix))
                .QueryAsync<TaskState>(
                    x => x.PartitionKey == partitionKey
                      && x.RowKey.CompareTo(rowKeyPrefix) >= 0
                      && x.RowKey.CompareTo(rowKeyPrefix + char.MaxValue) < 0)
                .ToListAsync(_telemetryClient.StartQueryLoopMetrics());
        }

        private async Task InsertAsync(IReadOnlyList<TaskState> taskStates)
        {
            foreach (var group in taskStates.GroupBy(x => new { x.StorageSuffix, x.PartitionKey }))
            {
                var table = await GetTableAsync(group.Key.StorageSuffix);
                var batch = new MutableTableTransactionalBatch(table);
                foreach (var taskState in taskStates)
                {
                    if (batch.Count >= StorageUtility.MaxBatchSize)
                    {
                        await batch.SubmitBatchAsync();
                        batch = new MutableTableTransactionalBatch(table);
                    }

                    batch.AddEntity(taskState);
                }

                await batch.SubmitBatchIfNotEmptyAsync();
            }
        }

        public async Task<int> GetCountLowerBoundAsync(string storageSuffix, string partitionKey)
        {
            return await (await GetTableAsync(storageSuffix))
                .GetEntityCountLowerBoundAsync(partitionKey, _telemetryClient.StartQueryLoopMetrics());
        }

        public async Task<TaskState> GetAsync(TaskStateKey key)
        {
            return await (await GetTableAsync(key.StorageSuffix))
                .GetEntityOrNullAsync<TaskState>(key.PartitionKey, key.RowKey);
        }

        public async Task DeleteAsync(TaskState taskState)
        {
            await (await GetTableAsync(taskState.StorageSuffix))
                .DeleteEntityAsync(taskState.PartitionKey, taskState.RowKey);
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
            return (await _serviceClientFactory.GetTableServiceClientAsync())
                .GetTableClient($"{_options.Value.TaskStateTableName}{suffix}");
        }
    }
}

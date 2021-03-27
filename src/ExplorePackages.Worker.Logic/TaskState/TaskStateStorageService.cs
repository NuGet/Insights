using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Worker
{
    public class TaskStateStorageService
    {
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly ITelemetryClient _telemetryClient;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;

        public TaskStateStorageService(
            ServiceClientFactory serviceClientFactory,
            ITelemetryClient telemetryClient,
            IOptions<ExplorePackagesWorkerSettings> options)
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
            await (await GetTableAsync(storageSuffix)).DeleteIfExistsAsync();
        }

        public async Task AddAsync(TaskStateKey taskStateKey)
        {
            await AddAsync(taskStateKey.StorageSuffix, taskStateKey.PartitionKey, new[] { taskStateKey.RowKey });
        }

        public async Task AddAsync(string storageSuffix, string partitionKey, IReadOnlyList<string> rowKeys)
        {
            await AddAsync(
                storageSuffix,
                partitionKey,
                rowKeys.Select(r => new TaskState(storageSuffix, partitionKey, r)).ToList());
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
                .GetEntityAsync<TaskState>(key.PartitionKey, key.RowKey);
        }

        public async Task DeleteAsync(TaskState taskState)
        {
            await (await GetTableAsync(taskState.StorageSuffix))
                .DeleteEntityAsync(taskState.PartitionKey, taskState.RowKey);
        }

        private async Task<TableClient> GetTableAsync(string suffix)
        {
            return (await _serviceClientFactory.GetTableServiceClientAsync())
                .GetTableClient($"{_options.Value.TaskStateTableName}{suffix}");
        }
    }
}

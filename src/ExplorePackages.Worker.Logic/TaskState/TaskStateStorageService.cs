using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage.Table;

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
            await GetTable(storageSuffix).CreateIfNotExistsAsync(retry: true);
        }

        public async Task DeleteTableAsync(string storageSuffix)
        {
            await GetTable(storageSuffix).DeleteIfExistsAsync();
        }

        public async Task<TaskState> GetOrAddAsync(TaskStateKey taskStateKey)
        {
            var added = await GetOrAddAsync(taskStateKey.StorageSuffix, taskStateKey.PartitionKey, new[] { taskStateKey.RowKey });
            return added.Single();
        }

        public async Task<TaskState> GetOrAddAsync(TaskState taskState)
        {
            var added = await GetOrAddAsync(taskState.StorageSuffix, taskState.PartitionKey, new[] { taskState });
            return added.Single();
        }

        public async Task<IReadOnlyList<TaskState>> GetOrAddAsync(string storageSuffix, string partitionKey, IReadOnlyList<string> rowKeys)
        {
            return await GetOrAddAsync(
                storageSuffix,
                partitionKey,
                rowKeys.Select(r => new TaskState(storageSuffix, partitionKey, r)).ToList());
        }

        public async Task<IReadOnlyList<TaskState>> GetOrAddAsync(string storageSuffix, string partitionKey, IReadOnlyList<TaskState> taskStates)
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

            toInsert.AddRange(existing.Where(x => existingRowKeys.Contains(x.RowKey)));

            return toInsert;
        }

        private async Task<IReadOnlyList<TaskState>> GetAllAsync(string storageSuffix, string partitionKey)
        {
            return await GetTable(storageSuffix).GetEntitiesAsync<TaskState>(partitionKey, _telemetryClient.StartQueryLoopMetrics());
        }

        private async Task InsertAsync(IReadOnlyList<TaskState> taskStates)
        {
            foreach (var group in taskStates.GroupBy(x => x.StorageSuffix))
            {
                await GetTable(group.Key).InsertEntitiesAsync(group.ToList());
            }
        }

        public async Task<int> GetCountLowerBoundAsync(string storageSuffix, string partitionKey)
        {
            return await GetTable(storageSuffix).GetEntityCountLowerBoundAsync<TaskState>(partitionKey, _telemetryClient.StartQueryLoopMetrics());
        }

        public async Task<TaskState> GetAsync(TaskStateKey key)
        {
            return await GetTable(key.StorageSuffix).RetrieveAsync<TaskState>(key.PartitionKey, key.RowKey);
        }

        public async Task ReplaceAsync(TaskState taskState)
        {
            await GetTable(taskState.StorageSuffix).ReplaceAsync(taskState);
        }

        public async Task DeleteAsync(TaskState taskState)
        {
            await GetTable(taskState.StorageSuffix).DeleteAsync(taskState);
        }

        private CloudTableClient GetClient()
        {
            return _serviceClientFactory
                .GetStorageAccount()
                .CreateCloudTableClient();
        }

        private CloudTable GetTable(string suffix)
        {
            return GetClient().GetTableReference($"{_options.Value.TaskStateTableName}{suffix}");
        }
    }
}

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
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;

        public TaskStateStorageService(
            ServiceClientFactory serviceClientFactory,
            IOptions<ExplorePackagesWorkerSettings> options)
        {
            _serviceClientFactory = serviceClientFactory;
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

        public async Task InitializeAllAsync(string storageSuffix, string partitionKey, IReadOnlyList<string> rowKeys)
        {
            var existing = await GetAllAsync(storageSuffix, partitionKey);

            await InsertAsync(rowKeys
                .Except(existing.Select(x => x.RowKey))
                .Select(r => new TaskState(storageSuffix, partitionKey, r))
                .ToList());
        }

        private async Task<IReadOnlyList<TaskState>> GetAllAsync(string storageSuffix, string partitionKey)
        {
            return await GetTable(storageSuffix).GetEntitiesAsync<TaskState>(partitionKey);
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
            return await GetTable(storageSuffix).GetEntityCountLowerBoundAsync<TaskState>(partitionKey);
        }

        public async Task<TaskState> GetAsync(string storageSuffix, string partitionKey, string rowKey)
        {
            return await GetTable(storageSuffix).RetrieveAsync<TaskState>(partitionKey, rowKey);
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

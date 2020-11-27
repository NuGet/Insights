using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.Worker
{
    public class CursorStorageService
    {
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IOptionsSnapshot<ExplorePackagesWorkerSettings> _options;

        public CursorStorageService(
            ServiceClientFactory serviceClientFactory,
            IOptionsSnapshot<ExplorePackagesWorkerSettings> options)
        {
            _serviceClientFactory = serviceClientFactory;
            _options = options;
        }

        public async Task InitializeAsync()
        {
            await GetTable().CreateIfNotExistsAsync(retry: true);
        }

        public async Task<CursorTableEntity> GetOrCreateAsync(string name)
        {
            var table = GetTable();
            var result = await table.ExecuteAsync(TableOperation.Retrieve<CursorTableEntity>(string.Empty, name));
            if (result.Result != null)
            {
                return (CursorTableEntity)result.Result;
            }
            else
            {
                var cursor = new CursorTableEntity(name);
                await table.ExecuteAsync(TableOperation.Insert(cursor));
                return cursor;
            }
        }

        public async Task UpdateAsync(CursorTableEntity cursor)
        {
            var table = GetTable();
            await table.ExecuteAsync(TableOperation.Replace(cursor));
        }

        private CloudTable GetTable()
        {
            return _serviceClientFactory
                .GetStorageAccount()
                .CreateCloudTableClient()
                .GetTableReference(_options.Value.CursorTableName);
        }
    }
}

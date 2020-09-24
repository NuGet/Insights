using System;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class CursorStorageService
    {
        private readonly ServiceClientFactory _serviceClientFactory;

        public CursorStorageService(ServiceClientFactory serviceClientFactory)
        {
            _serviceClientFactory = serviceClientFactory;
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
                .GetTableReference("cursors");
        }
    }
}

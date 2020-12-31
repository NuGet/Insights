using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.Worker
{
    public class CursorStorageService
    {
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;
        private readonly ILogger<CursorStorageService> _logger;

        public CursorStorageService(
            ServiceClientFactory serviceClientFactory,
            IOptions<ExplorePackagesWorkerSettings> options,
            ILogger<CursorStorageService> logger)
        {
            _serviceClientFactory = serviceClientFactory;
            _options = options;
            _logger = logger;
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
                _logger.LogInformation("Creating cursor {Name} to timestamp {Value:O}.", name, cursor.Value);
                await table.ExecuteAsync(TableOperation.Insert(cursor));
                return cursor;
            }
        }

        public async Task UpdateAsync(CursorTableEntity cursor)
        {
            var table = GetTable();
            _logger.LogInformation("Updating cursor {Name} to timestamp {NewValue:O}.", cursor.Name, cursor.Value);
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

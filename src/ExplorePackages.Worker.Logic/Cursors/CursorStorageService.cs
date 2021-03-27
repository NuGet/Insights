using System.Threading.Tasks;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
            await (await GetTableAsync()).CreateIfNotExistsAsync(retry: true);
        }

        public async Task<CursorTableEntity> GetOrCreateAsync(string name)
        {
            var table = await GetTableAsync();
            var cursor = await table.GetEntityOrNullAsync<CursorTableEntity>(string.Empty, name);
            if (cursor != null)
            {
                return cursor;
            }
            else
            {
                cursor = new CursorTableEntity(name);
                _logger.LogInformation("Creating cursor {Name} to timestamp {Value:O}.", name, cursor.Value);
                var response = await table.AddEntityAsync(cursor);
                cursor.UpdateETagAndTimestamp(response);
                return cursor;
            }
        }

        public async Task UpdateAsync(CursorTableEntity cursor)
        {
            var table = await GetTableAsync();
            _logger.LogInformation("Updating cursor {Name} to timestamp {NewValue:O}.", cursor.GetName(), cursor.Value);
            var response = await table.UpdateEntityAsync(cursor, cursor.ETag);
            cursor.UpdateETagAndTimestamp(response);
        }

        private async Task<TableClient> GetTableAsync()
        {
            return (await _serviceClientFactory.GetTableServiceClientAsync())
                .GetTableClient(_options.Value.CursorTableName);
        }
    }
}

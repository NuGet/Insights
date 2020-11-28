using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages
{
    public class CloudTableWrapper : ICloudTable
    {
        private readonly CloudTable _inner;

        public CloudTableWrapper(CloudTable inner)
        {
            _inner = inner;
        }

        public async Task CreateIfNotExistsAsync() => await _inner.CreateIfNotExistsAsync();
        public async Task DeleteIfExistsAsync() => await _inner.DeleteIfExistsAsync();

        public async Task<TableResult> ExecuteAsync(TableOperation operation)
        {
            return await _inner.ExecuteAsync(operation);
        }

        public async Task<IList<TableResult>> ExecuteBatchAsync(TableBatchOperation batch)
        {
            return await _inner.ExecuteBatchAsync(batch);
        }

        public async Task<TableQuerySegment<T>> ExecuteQuerySegmentedAsync<T>(TableQuery<T> query, TableContinuationToken currentToken) where T : ITableEntity, new()
        {
            return await _inner.ExecuteQuerySegmentedAsync(query, currentToken);
        }
    }
}

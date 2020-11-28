using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages
{
    public interface ICloudTable
    {
        Task CreateIfNotExistsAsync();
        Task DeleteIfExistsAsync();
        Task<TableResult> ExecuteAsync(TableOperation operation);
        Task<IList<TableResult>> ExecuteBatchAsync(TableBatchOperation batch);
        Task<TableQuerySegment<T>> ExecuteQuerySegmentedAsync<T>(TableQuery<T> query, TableContinuationToken currentToken) where T : ITableEntity, new();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;
using static Knapcode.ExplorePackages.Worker.StorageUtility;

namespace Knapcode.ExplorePackages.Worker
{
    public static class CloudTableExtensions
    {
        public static async Task<IReadOnlyList<T>> GetEntitiesAsync<T>(this CloudTable table, string partitionKey, int? maxEntities = null) where T : ITableEntity, new()
        {
            return await GetEntitiesAsync<T>(table.ExecuteQuerySegmentedAsync, partitionKey, maxEntities);
        }

        public static async Task<IReadOnlyList<T>> GetEntitiesAsync<T>(this ICloudTable table, string partitionKey, int? maxEntities = null) where T : ITableEntity, new()
        {
            return await GetEntitiesAsync<T>(table.ExecuteQuerySegmentedAsync, partitionKey, maxEntities);
        }

        private static async Task<IReadOnlyList<T>> GetEntitiesAsync<T>(
            Func<TableQuery<T>, TableContinuationToken, Task<TableQuerySegment<T>>> executeQuerySegmentedAsync,
            string partitionKey,
            int? maxEntities = null) where T : ITableEntity, new()
        {
            var entities = new List<T>();
            var query = new TableQuery<T>
            {
                FilterString = TableQuery.GenerateFilterCondition(
                    PartitionKey,
                    QueryComparisons.Equal,
                    partitionKey),
                TakeCount = MaxTakeCount,
            };

            TableContinuationToken token = null;
            do
            {
                query.TakeCount = Math.Min(MaxTakeCount, maxEntities.GetValueOrDefault(MaxTakeCount) - entities.Count);
                var segment = await executeQuerySegmentedAsync(query, token);
                token = segment.ContinuationToken;
                entities.AddRange(segment.Results);
            }
            while (token != null && (!maxEntities.HasValue || entities.Count < maxEntities));

            return entities;
        }

        public static async Task InsertEntitiesAsync<T>(this CloudTable table, IReadOnlyList<T> entities) where T : ITableEntity
        {
            await InsertEntitiesAsync(table.ExecuteBatchAsync, entities);
        }

        public static async Task InsertEntitiesAsync<T>(this ICloudTable table, IReadOnlyList<T> entities) where T : ITableEntity
        {
            await InsertEntitiesAsync(table.ExecuteBatchAsync, entities);
        }

        private static async Task InsertEntitiesAsync<T>(Func<TableBatchOperation, Task> executeBatchAsync, IReadOnlyList<T> entities) where T : ITableEntity
        {
            if (!entities.Any())
            {
                return;
            }

            var batch = new TableBatchOperation();
            foreach (var scan in entities)
            {
                if (batch.Count >= MaxBatchSize)
                {
                    await executeBatchAsync(batch);
                    batch = new TableBatchOperation();
                }

                batch.Add(TableOperation.Insert(scan));
            }

            if (batch.Count > 0)
            {
                await executeBatchAsync(batch);
            }
        }

        public static async Task<int> GetEntityCountLowerBoundAsync<T>(this CloudTable table, string partitionKey) where T : ITableEntity, new()
        {
            var query = new TableQuery<T>
            {
                FilterString = TableQuery.GenerateFilterCondition(
                    PartitionKey,
                    QueryComparisons.Equal,
                    partitionKey),
                TakeCount = MaxTakeCount,
                SelectColumns = Array.Empty<string>(),
            };

            TableContinuationToken token = null;
            do
            {
                var segment = await table.ExecuteQuerySegmentedAsync<T>(query, token);
                token = segment.ContinuationToken;

                if (segment.Results.Count > 0)
                {
                    return segment.Results.Count;
                }
            }
            while (token != null);

            return 0;
        }

        public static async Task<T> RetrieveAsync<T>(this CloudTable table, string partitionKey, string rowKey) where T : class, ITableEntity
        {
            var result = await table.ExecuteAsync(TableOperation.Retrieve<T>(partitionKey, rowKey));
            return result.Result != null ? (T)result.Result : null;
        }

        public static async Task ReplaceAsync<T>(this CloudTable table, T entity) where T : ITableEntity
        {
            await table.ExecuteAsync(TableOperation.Replace(entity));
        }

        public static async Task DeleteAsync<T>(this CloudTable table, T entity) where T : ITableEntity
        {
            await table.ExecuteAsync(TableOperation.Delete(entity));
        }
    }
}

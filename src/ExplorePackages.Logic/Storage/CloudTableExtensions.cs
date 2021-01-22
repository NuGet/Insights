using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;
using static Knapcode.ExplorePackages.StorageUtility;

namespace Knapcode.ExplorePackages
{
    public static class CloudTableExtensions
    {
        public static async Task<IReadOnlyList<T>> GetEntitiesAsync<T>(this CloudTable table, string partitionKey, string minRowKey, string maxRowKey, QueryLoopMetrics metrics) where T : ITableEntity, new()
        {
            return await GetEntitiesAsync<T>(table.ExecuteQuerySegmentedAsync, partitionKey, minRowKey, maxRowKey, metrics, maxEntities: null);
        }

        public static async Task<IReadOnlyList<T>> GetEntitiesAsync<T>(this CloudTable table, QueryLoopMetrics metrics) where T : ITableEntity, new()
        {
            return await GetEntitiesAsync<T>(table.ExecuteQuerySegmentedAsync, partitionKey: null, minRowKey: null, maxRowKey: null, metrics, maxEntities: null);
        }

        public static async Task<IReadOnlyList<T>> GetEntitiesAsync<T>(this CloudTable table, string partitionKey, QueryLoopMetrics metrics, int? maxEntities = null) where T : ITableEntity, new()
        {
            return await GetEntitiesAsync<T>(table.ExecuteQuerySegmentedAsync, partitionKey, minRowKey: null, maxRowKey: null, metrics, maxEntities);
        }

        public static async Task<IReadOnlyList<T>> GetEntitiesAsync<T>(this ICloudTable table, string partitionKey, QueryLoopMetrics metrics, int? maxEntities = null) where T : ITableEntity, new()
        {
            return await GetEntitiesAsync<T>(table.ExecuteQuerySegmentedAsync, partitionKey, minRowKey: null, maxRowKey: null, metrics, maxEntities);
        }

        private static async Task<IReadOnlyList<T>> GetEntitiesAsync<T>(
            Func<TableQuery<T>, TableContinuationToken, Task<TableQuerySegment<T>>> executeQuerySegmentedAsync,
            string partitionKey,
            string minRowKey,
            string maxRowKey,
            QueryLoopMetrics metrics,
            int? maxEntities = null) where T : ITableEntity, new()
        {
            using (metrics)
            {
                var entities = new List<T>();
                var query = new TableQuery<T>
                {
                    TakeCount = MaxTakeCount,
                };

                if (partitionKey != null)
                {
                    query.FilterString = TableQuery.GenerateFilterCondition(
                        PartitionKey,
                        QueryComparisons.Equal,
                        partitionKey);

                    string rowKeyFilterString = null;
                    if (minRowKey != null)
                    {
                        rowKeyFilterString = TableQuery.GenerateFilterCondition(
                            RowKey,
                            QueryComparisons.GreaterThanOrEqual,
                            minRowKey);
                    }

                    if (maxRowKey != null)
                    {
                        var maxRowKeyFilterString = TableQuery.GenerateFilterCondition(
                            RowKey,
                            QueryComparisons.LessThanOrEqual,
                            maxRowKey);

                        if (rowKeyFilterString != null)
                        {
                            rowKeyFilterString = TableQuery.CombineFilters(rowKeyFilterString, TableOperators.And, maxRowKeyFilterString);
                        }
                        else
                        {
                            rowKeyFilterString = maxRowKeyFilterString;
                        }
                    }

                    if (rowKeyFilterString != null)
                    {
                        query.FilterString = TableQuery.CombineFilters(query.FilterString, TableOperators.And, rowKeyFilterString);
                    }
                }

                TableContinuationToken token = null;
                do
                {
                    if (maxEntities.HasValue)
                    {
                        var remaining = maxEntities.Value - entities.Count;
                        query.TakeCount = Math.Min(MaxTakeCount, remaining);
                    }

                    TableQuerySegment<T> segment;
                    using (metrics.TrackQuery())
                    {
                        segment = await executeQuerySegmentedAsync(query, token);
                    }

                    token = segment.ContinuationToken;
                    entities.AddRange(segment.Results);
                }
                while (token != null && (!maxEntities.HasValue || entities.Count < maxEntities));

                return entities;
            }
        }

        public static async Task InsertEntitiesAsync<T>(this CloudTable table, IReadOnlyList<T> entities) where T : ITableEntity
        {
            await InsertEntitiesAsync(table.ExecuteBatchAsync, entities);
        }

        public static async Task InsertEntitiesAsync<T>(this ICloudTable table, IReadOnlyList<T> entities) where T : ITableEntity
        {
            await InsertEntitiesAsync(table.ExecuteBatchAsync, entities);
        }

        public static async Task InsertOrReplaceEntitiesAsync<T>(this CloudTable table, IReadOnlyList<T> entities) where T : ITableEntity
        {
            await InsertOrReplaceEntitiesAsync(table.ExecuteBatchAsync, entities);
        }

        public static async Task InsertOrReplaceEntitiesAsync<T>(this ICloudTable table, IReadOnlyList<T> entities) where T : ITableEntity
        {
            await InsertOrReplaceEntitiesAsync(table.ExecuteBatchAsync, entities);
        }

        private static async Task InsertEntitiesAsync<T>(Func<TableBatchOperation, Task> executeBatchAsync, IReadOnlyList<T> entities) where T : ITableEntity
        {
            await BatchEntities(TableOperation.Insert, executeBatchAsync, entities);
        }

        private static async Task InsertOrReplaceEntitiesAsync<T>(Func<TableBatchOperation, Task> executeBatchAsync, IReadOnlyList<T> entities) where T : ITableEntity
        {
            await BatchEntities(TableOperation.InsertOrReplace, executeBatchAsync, entities);
        }

        private static async Task BatchEntities<T>(Func<ITableEntity, TableOperation> getOperation, Func<TableBatchOperation, Task> executeBatchAsync, IReadOnlyList<T> entities) where T : ITableEntity
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

                batch.Add(getOperation(scan));
            }

            if (batch.Count > 0)
            {
                await executeBatchAsync(batch);
            }
        }

        public static async Task<int> GetEntityCountLowerBoundAsync<T>(this CloudTable table, string partitionKey, QueryLoopMetrics metrics) where T : ITableEntity, new()
        {
            return await GetEntityCountLowerBoundWithFilterAsync<T>(
                table,
                TableQuery.GenerateFilterCondition(
                    PartitionKey,
                    QueryComparisons.Equal,
                    partitionKey),
                metrics);
        }

        public static async Task<int> GetEntityCountLowerBoundAsync<T>(this CloudTable table, string minPartitionKey, string maxPartitionKey, QueryLoopMetrics metrics) where T : ITableEntity, new()
        {
            return await table.GetEntityCountLowerBoundWithFilterAsync<T>(
                TableQuery.CombineFilters(
                    TableQuery.GenerateFilterCondition(
                        PartitionKey,
                        QueryComparisons.GreaterThanOrEqual,
                        minPartitionKey),
                    TableOperators.And,
                    TableQuery.GenerateFilterCondition(
                        PartitionKey,
                        QueryComparisons.LessThanOrEqual,
                        maxPartitionKey)),
                metrics);
        }

        private static async Task<int> GetEntityCountLowerBoundWithFilterAsync<T>(this CloudTable table, string filterString, QueryLoopMetrics metrics) where T : ITableEntity, new()
        {
            using (metrics)
            {
                var query = new TableQuery<T>
                {
                    FilterString = filterString,
                    TakeCount = MaxTakeCount,
                    SelectColumns = Array.Empty<string>(),
                };

                TableContinuationToken token = null;
                do
                {
                    TableQuerySegment<T> segment;
                    using (metrics.TrackQuery())
                    {
                        segment = await table.ExecuteQuerySegmentedAsync(query, token);
                    }

                    token = segment.ContinuationToken;

                    if (segment.Results.Count > 0)
                    {
                        return segment.Results.Count;
                    }
                }
                while (token != null);

                return 0;
            }
        }

        public static async Task<T> RetrieveAsync<T>(this CloudTable table, string partitionKey, string rowKey) where T : ITableEntity
        {
            var result = await table.ExecuteAsync(TableOperation.Retrieve<T>(partitionKey, rowKey));
            return result.Result != null ? (T)result.Result : default;
        }

        public static async Task ReplaceAsync<T>(this CloudTable table, T entity) where T : ITableEntity
        {
            await table.ExecuteAsync(TableOperation.Replace(entity));
        }

        public static async Task InsertOrReplaceAsync<T>(this CloudTable table, T entity) where T : ITableEntity
        {
            await table.ExecuteAsync(TableOperation.InsertOrReplace(entity));
        }

        public static async Task DeleteAsync<T>(this CloudTable table, T entity) where T : ITableEntity
        {
            await table.ExecuteAsync(TableOperation.Delete(entity));
        }
    }
}

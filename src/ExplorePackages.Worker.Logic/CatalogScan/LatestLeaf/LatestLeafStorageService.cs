using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using static Knapcode.ExplorePackages.StorageUtility;

namespace Knapcode.ExplorePackages.Worker
{
    public class LatestLeafStorageService<T> where T : ILatestPackageLeaf, new()
    {
        private readonly ITelemetryClient _telemetryClient;
        private readonly ILogger<LatestLeafStorageService<T>> _logger;

        public LatestLeafStorageService(
            ITelemetryClient telemetryClient,
            ILogger<LatestLeafStorageService<T>> logger)
        {
            _telemetryClient = telemetryClient;
            _logger = logger;
        }

        public async Task AddAsync(
            string packageId,
            IReadOnlyList<CatalogLeafItem> items,
            ILatestPackageLeafStorage<T> storage,
            bool allowRetries)
        {
            var maxAttempts = allowRetries ? 5 : 1;
            var attempt = 0;
            while (true)
            {
                attempt++;
                try
                {
                    await AddAsync(packageId, items, storage);
                    break;
                }
                catch (StorageException ex) when (attempt < maxAttempts
                    && (ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.Conflict
                        || ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.PreconditionFailed))
                {
                    _logger.LogWarning(
                        ex,
                        "Attempt {Attempt}: adding entities for package ID {PackageId} failed due to an HTTP {StatusCode}. Trying again.",
                        attempt,
                        packageId,
                        ex.RequestInformation.HttpStatusCode);
                }
            }
        }

        private async Task AddAsync(string packageId, IReadOnlyList<CatalogLeafItem> items, ILatestPackageLeafStorage<T> storage)
        {
            (var rowKeyToItem, var rowKeyToETag) = await GetExistingsRowsAsync(packageId, items, storage);

            // Add the row keys that remain. These are the versions that are not in Table Storage or are newer than the
            // commit timestamp in Table Storage.
            var rowKeysToUpsert = new List<string>();
            rowKeysToUpsert.AddRange(rowKeyToItem.Keys);
            rowKeysToUpsert.Sort(StringComparer.Ordinal);

            // Update or insert the rows.
            var batch = new TableBatchOperation();
            for (var i = 0; i < rowKeysToUpsert.Count; i++)
            {
                if (batch.Count >= MaxBatchSize)
                {
                    await ExecuteBatchAsync(storage.Table, batch);
                    batch = new TableBatchOperation();
                }

                var rowKey = rowKeysToUpsert[i];
                var leaf = rowKeyToItem[rowKey];
                var entity = await storage.MapAsync(leaf);

                if (rowKeyToETag.TryGetValue(rowKey, out var etag))
                {
                    entity.ETag = etag;
                    batch.Add(TableOperation.Replace(entity));
                }
                else
                {
                    batch.Add(TableOperation.Insert(entity));
                }
            }

            if (batch.Count > 0)
            {
                await ExecuteBatchAsync(storage.Table, batch);
            }
        }

        private async Task<(Dictionary<string, CatalogLeafItem> RowKeyToItem, Dictionary<string, string> RowKeyToETag)> GetExistingsRowsAsync(
            string packageId,
            IEnumerable<CatalogLeafItem> items,
            ILatestPackageLeafStorage<T> storage)
        {
            using var metrics = _telemetryClient.StartQueryLoopMetrics();

            // Sort items by lexicographical order, since this is what table storage does.
            var itemList = items
                .Select(x => new { Item = x, RowKey = storage.GetRowKey(x.PackageVersion) })
                .GroupBy(x => x.RowKey)
                .Select(x => x.OrderByDescending(x => x.Item.CommitTimestamp).First())
                .OrderBy(x => x.RowKey, StringComparer.Ordinal)
                .Select(x => (x.Item, x.RowKey))
                .ToList();
            var rowKeyToItem = itemList.ToDictionary(x => x.RowKey, x => x.Item);
            var rowKeyToEtag = new Dictionary<string, string>();

            // Query for all of the version data in Table Storage, determining what needs to be updated.
            var filterString = TableQuery.CombineFilters(
                TableQuery.GenerateFilterCondition(
                    PartitionKey,
                    QueryComparisons.Equal,
                    storage.GetPartitionKey(packageId)),
                TableOperators.And,
                TableQuery.CombineFilters(
                    TableQuery.GenerateFilterCondition(
                        RowKey,
                        QueryComparisons.GreaterThanOrEqual,
                        itemList.First().RowKey),
                    TableOperators.And,
                    TableQuery.GenerateFilterCondition(
                        RowKey,
                        QueryComparisons.LessThanOrEqual,
                        itemList.Last().RowKey)));

            var query = new TableQuery<T>
            {
                FilterString = filterString,
                SelectColumns = new List<string> { RowKey, storage.CommitTimestampColumnName },
                TakeCount = MaxTakeCount,
            };

            TableContinuationToken token = null;
            do
            {
                TableQuerySegment<T> segment;
                using (metrics.TrackQuery())
                {
                    segment = await storage.Table.ExecuteQuerySegmentedAsync(query, token);
                }

                token = segment.ContinuationToken;

                foreach (var result in segment.Results)
                {
                    if (rowKeyToItem.TryGetValue(result.RowKey, out var item))
                    {
                        if (result.CommitTimestamp >= item.CommitTimestamp)
                        {
                            // The version in Table Storage is newer, ignore the version we have.
                            rowKeyToItem.Remove(result.RowKey);
                        }
                        else
                        {
                            // The version in Table Storage is older, save the etag to update it.
                            rowKeyToEtag.Add(result.RowKey, result.ETag);
                        }
                    }
                }
            }
            while (token != null);

            return (rowKeyToItem, rowKeyToEtag);
        }

        private async Task ExecuteBatchAsync(CloudTable table, TableBatchOperation batch)
        {
            _logger.LogInformation("Upserting {Count} latest package leaf rows of type {T} into {TableName}.", batch.Count, typeof(T).FullName, table.Name);
            await table.ExecuteBatchAsync(batch);
        }
    }
}

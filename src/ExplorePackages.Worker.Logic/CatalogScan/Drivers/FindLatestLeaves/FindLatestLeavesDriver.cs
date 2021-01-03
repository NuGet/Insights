using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using static Knapcode.ExplorePackages.StorageUtility;

namespace Knapcode.ExplorePackages.Worker.FindLatestLeaves
{
    public class FindLatestLeavesDriver<T> : ICatalogScanDriver where T : ILatestPackageLeaf, new()
    {
        private readonly CatalogClient _catalogClient;
        private readonly ILatestPackageLeafStorageFactory<T> _storageFactory;
        private readonly ITelemetryClient _telemetryClient;
        private readonly ILogger<FindLatestLeavesDriver<T>> _logger;

        public FindLatestLeavesDriver(
            CatalogClient catalogClient,
            ILatestPackageLeafStorageFactory<T> storageFactory,
            ITelemetryClient telemetryClient,
            ILogger<FindLatestLeavesDriver<T>> logger)
        {
            _catalogClient = catalogClient;
            _storageFactory = storageFactory;
            _telemetryClient = telemetryClient;
            _logger = logger;
        }

        public async Task<CatalogIndexScanResult> ProcessIndexAsync(CatalogIndexScan indexScan)
        {
            await _storageFactory.InitializeAsync(indexScan);

            return CatalogIndexScanResult.ExpandAllLeaves;
        }

        public async Task<CatalogPageScanResult> ProcessPageAsync(CatalogPageScan pageScan)
        {
            var page = await _catalogClient.GetCatalogPageAsync(pageScan.Url);
            var items = page.GetLeavesInBounds(pageScan.Min, pageScan.Max, excludeRedundantLeaves: true);

            // Prune leaf items outside of the timestamp bounds to avoid issues with out-of-bound leaves being processed.
            var leafItemToRank = page.GetLeafItemToRank();
            leafItemToRank = items.ToDictionary(x => x, x => leafItemToRank[x]);

            var storage = await _storageFactory.CreateAsync(pageScan, leafItemToRank);

            await AddAsync(items, storage);

            return CatalogPageScanResult.Processed;
        }

        private async Task AddAsync(List<CatalogLeafItem> items, ILatestPackageLeafStorage<T> storage)
        {
            var packageIdGroups = items.GroupBy(x => x.PackageId, StringComparer.OrdinalIgnoreCase);
            foreach (var group in packageIdGroups)
            {
                await AddAsync(group.Key, group, storage);
            }
        }

        private async Task AddAsync(string packageId, IEnumerable<CatalogLeafItem> items, ILatestPackageLeafStorage<T> storage)
        {
            (var rowKeyToItem, var rowKeyToETag) = await GetExistingsRowsAsync(packageId, items, storage);

            // Add the row keys that remain. These are the versions that are not in Table Storage.
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
                var entity = storage.Map(leaf);

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
            List<(CatalogLeafItem Item, string RowKey)> itemList = items
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
                        itemList.First().RowKey)));

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

        public Task<DriverResult> ProcessLeafAsync(CatalogLeafScan leafScan) => throw new NotImplementedException();
        public Task StartAggregateAsync(CatalogIndexScan indexScan) => Task.CompletedTask;
        public Task<bool> IsAggregateCompleteAsync(CatalogIndexScan indexScan) => Task.FromResult(true);
        public Task FinalizeAsync(CatalogIndexScan indexScan) => Task.CompletedTask;
    }
}

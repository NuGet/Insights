using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using static Knapcode.ExplorePackages.StorageUtility;

namespace Knapcode.ExplorePackages.Worker.FindLatestLeaves
{
    public class LatestPackageLeafStorageService
    {
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly ITelemetryClient _telemetryClient;
        private readonly ILogger<LatestPackageLeafStorageService> _logger;

        public LatestPackageLeafStorageService(
            ServiceClientFactory serviceClientFactory,
            ITelemetryClient telemetryClient,
            ILogger<LatestPackageLeafStorageService> logger)
        {
            _serviceClientFactory = serviceClientFactory;
            _telemetryClient = telemetryClient;
            _logger = logger;
        }

        public async Task InitializeAsync(string tableName)
        {
            await GetTable(tableName).CreateIfNotExistsAsync(retry: true);
        }

        public async Task AddAsync(
            string tableName,
            string prefix,
            IReadOnlyList<CatalogLeafItem> items,
            IReadOnlyDictionary<CatalogLeafItem, int> leafItemToRank,
            int pageRank,
            string pageUrl)
        {
            var table = GetTable(tableName);
            var packageIdGroups = items.GroupBy(x => x.PackageId, StringComparer.OrdinalIgnoreCase);
            foreach (var group in packageIdGroups)
            {
                await AddAsync(table, prefix, group.Key, group, leafItemToRank, pageRank, pageUrl);
            }
        }

        private async Task AddAsync(
            CloudTable table,
            string prefix,
            string packageId,
            IEnumerable<CatalogLeafItem> items,
            IReadOnlyDictionary<CatalogLeafItem, int> leafItemToRank,
            int pageRank,
            string pageUrl)
        {
            (var lowerVersionToItem, var lowerVersionToEtag) = await GetExistingsRowsAsync(table, prefix, packageId, items);

            // Add the versions that remain. These are the versions that are not in Table Storage.
            var versionsToUpsert = new List<string>();
            versionsToUpsert.AddRange(lowerVersionToItem.Keys);
            versionsToUpsert.Sort(StringComparer.Ordinal);

            // Update or insert the rows.
            var batch = new TableBatchOperation();
            for (var i = 0; i < versionsToUpsert.Count; i++)
            {
                if (batch.Count >= MaxBatchSize)
                {
                    await ExecuteBatchAsync(table, batch);
                    batch = new TableBatchOperation();
                }

                var lowerVersion = versionsToUpsert[i];
                var leaf = lowerVersionToItem[lowerVersion];
                var entity = new LatestPackageLeaf(prefix, leaf, leafItemToRank[leaf], pageRank, pageUrl);

                if (lowerVersionToEtag.TryGetValue(lowerVersion, out var etag))
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
                await ExecuteBatchAsync(table, batch);
            }
        }

        private async Task<(Dictionary<string, CatalogLeafItem>, Dictionary<string, string>)> GetExistingsRowsAsync(CloudTable table, string prefix, string packageId, IEnumerable<CatalogLeafItem> items)
        {
            using var metrics = _telemetryClient.NewQueryLoopMetrics();

            // Sort items by lexicographical order, since this is what table storage does.
            List<(CatalogLeafItem Item, string LowerVersion)> itemList = items
                .Select(x => new { Item = x, LowerVersion = LatestPackageLeaf.GetRowKey(x.PackageVersion) })
                .GroupBy(x => x.LowerVersion)
                .Select(x => x.OrderByDescending(x => x.Item.CommitTimestamp).First())
                .OrderBy(x => x.LowerVersion, StringComparer.Ordinal)
                .Select(x => (x.Item, x.LowerVersion))
                .ToList();
            var lowerVersionToItem = itemList.ToDictionary(x => x.LowerVersion, x => x.Item);
            var lowerVersionToEtag = new Dictionary<string, string>();

            // Query for all of the version data in Table Storage, determining what needs to be updated.
            var filterString = TableQuery.CombineFilters(
                EqualPrefixAndPackageId(prefix, packageId),
                TableOperators.And,
                TableQuery.CombineFilters(
                    GreaterThanOrEqualToVersion(itemList.First().LowerVersion),
                    TableOperators.And,
                    LessThanOrEqualToVersion(itemList.Last().LowerVersion)));
            var query = new TableQuery<LatestPackageLeaf>
            {
                FilterString = filterString,
                SelectColumns = new List<string> { RowKey, nameof(LatestPackageLeaf.CommitTimestamp) },
                TakeCount = MaxTakeCount,
            };

            TableContinuationToken token = null;
            do
            {
                TableQuerySegment<LatestPackageLeaf> segment;
                using (metrics.TrackQuery())
                {
                    segment = await table.ExecuteQuerySegmentedAsync(query, token);
                }

                token = segment.ContinuationToken;

                foreach (var result in segment.Results)
                {
                    if (lowerVersionToItem.TryGetValue(result.LowerVersion, out var item))
                    {
                        if (result.CommitTimestamp >= item.CommitTimestamp)
                        {
                            // The version in Table Storage is newer, ignore the version we have.
                            lowerVersionToItem.Remove(result.LowerVersion);
                        }
                        else
                        {
                            // The version in Table Storage is older, save the etag to update it.
                            lowerVersionToEtag.Add(result.LowerVersion, result.ETag);
                        }
                    }
                }
            }
            while (token != null);

            return (lowerVersionToItem, lowerVersionToEtag);
        }

        private async Task ExecuteBatchAsync(CloudTable table, TableBatchOperation batch)
        {
            _logger.LogInformation("Upserting {Count} latest package leaf rows into {TableName}.", batch.Count, table.Name);
            await table.ExecuteBatchAsync(batch);
        }

        private static string EqualPrefixAndPackageId(string prefix, string id)
        {
            return TableQuery.GenerateFilterCondition(
                PartitionKey,
                QueryComparisons.Equal,
                LatestPackageLeaf.GetPartitionKey(prefix, id));
        }

        private static string GreaterThanOrEqualToVersion(string lowerVersion)
        {
            return TableQuery.GenerateFilterCondition(RowKey, QueryComparisons.GreaterThanOrEqual, lowerVersion);
        }

        private static string LessThanOrEqualToVersion(string lowerVersion)
        {
            return TableQuery.GenerateFilterCondition(RowKey, QueryComparisons.LessThanOrEqual, lowerVersion);
        }

        private CloudTable GetTable(string tableName)
        {
            return _serviceClientFactory
                .GetStorageAccount()
                .CreateCloudTableClient()
                .GetTableReference(tableName);
        }
    }
}

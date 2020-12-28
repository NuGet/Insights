using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage.Table;
using static Knapcode.ExplorePackages.StorageUtility;

namespace Knapcode.ExplorePackages.Worker.FindLatestLeaves
{
    public class LatestPackageLeafStorageService
    {
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;
        private readonly ILogger<LatestPackageLeafStorageService> _logger;

        public LatestPackageLeafStorageService(
            ServiceClientFactory serviceClientFactory,
            IOptions<ExplorePackagesWorkerSettings> options,
            ILogger<LatestPackageLeafStorageService> logger)
        {
            _serviceClientFactory = serviceClientFactory;
            _options = options;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await GetTable().CreateIfNotExistsAsync(retry: true);
        }

        public async Task Sandbox()
        {
            var prefix = string.Empty;
            var packageIdPrefix = "microsoft.e";

            var partitionKeyPrefix = $"{prefix}${packageIdPrefix}";
            var table = GetTable();

            var enumerator = new TablePrefixScanner();
            await enumerator.EnumerateAllByPrefixAsync<LatestPackageLeaf>(table, partitionKeyPrefix, selectColumns: null);
        }

        private static void WriteResults<T>(string path, List<T> results) where T : ITableEntity
        {
            File.WriteAllLines(path, results.Select(x => $"{x.PartitionKey}\t{x.RowKey}"));
        }

        public async Task AddAsync(string prefix, IReadOnlyList<CatalogLeafItem> items)
        {
            var table = GetTable();
            var packageIdGroups = items.GroupBy(x => x.PackageId, StringComparer.OrdinalIgnoreCase);
            foreach (var group in packageIdGroups)
            {
                await AddAsync(table, prefix, group.Key, group);
            }
        }

        public async Task AddAsync(CloudTable table, string prefix, string packageId, IEnumerable<CatalogLeafItem> items)
        {
            // Sort items by lexicographical order, since this is what table storage does.
            var itemList = items
                .Select(x => new { Item = x, LowerVersion = LatestPackageLeaf.GetRowKey(x.PackageVersion) })
                .GroupBy(x => x.LowerVersion)
                .Select(x => x.OrderByDescending(x => x.Item.CommitTimestamp).First())
                .OrderBy(x => x.LowerVersion, StringComparer.Ordinal)
                .ToList();
            var lowerVersionToItem = itemList.ToDictionary(x => x.LowerVersion, x => x.Item);
            var lowerVersionToEtag = new Dictionary<string, string>();
            var versionsToUpsert = new List<string>();

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
                var segment = await table.ExecuteQuerySegmentedAsync(query, token);
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

            // Add the versions that remain. These are the versions that are not in Table Storage.
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
                var entity = new LatestPackageLeaf(prefix, leaf)
                {
                    CommitTimestamp = leaf.CommitTimestamp,
                    ParsedLeafType = leaf.Type,
                    Url = leaf.Url,
                };

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

        private CloudTable GetTable()
        {
            return _serviceClientFactory
                .GetStorageAccount()
                .CreateCloudTableClient()
                .GetTableReference(_options.Value.LatestLeavesTableName);
        }
    }
}

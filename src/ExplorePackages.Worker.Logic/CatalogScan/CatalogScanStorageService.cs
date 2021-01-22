using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogScanStorageService
    {
        private static readonly IReadOnlyDictionary<string, CatalogLeafScan> EmptyLeafIdToLeafScans = new Dictionary<string, CatalogLeafScan>();

        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly ITelemetryClient _telemetryClient;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;

        public CatalogScanStorageService(
            ServiceClientFactory serviceClientFactory,
            ITelemetryClient telemetryClient,
            IOptions<ExplorePackagesWorkerSettings> options)
        {
            _serviceClientFactory = serviceClientFactory;
            _telemetryClient = telemetryClient;
            _options = options;
        }

        public async Task InitializeAsync()
        {
            await GetIndexScanTable().CreateIfNotExistsAsync(retry: true);
        }

        public async Task InitializeChildTablesAsync(string storageSuffix)
        {
            await GetPageScanTable(storageSuffix).CreateIfNotExistsAsync(retry: true);
            await GetLeafScanTable(storageSuffix).CreateIfNotExistsAsync(retry: true);
        }

        public async Task DeleteChildTablesAsync(string storageSuffix)
        {
            await GetLeafScanTable(storageSuffix).DeleteIfExistsAsync();
            await GetPageScanTable(storageSuffix).DeleteIfExistsAsync();
        }

        public async Task InsertAsync(CatalogIndexScan indexScan)
        {
            var table = GetIndexScanTable();
            await table.ExecuteAsync(TableOperation.Insert(indexScan));
        }

        public async Task<IReadOnlyList<CatalogPageScan>> GetPageScansAsync(string storageSuffix, string scanId)
        {
            return await GetPageScanTable(storageSuffix).GetEntitiesAsync<CatalogPageScan>(scanId, _telemetryClient.StartQueryLoopMetrics());
        }

        public async Task<IReadOnlyList<CatalogLeafScan>> GetLeafScansAsync(string storageSuffix, string scanId, string pageId)
        {
            return await GetLeafScanTable(storageSuffix).GetEntitiesAsync<CatalogLeafScan>(
                CatalogLeafScan.GetPartitionKey(scanId, pageId),
                _telemetryClient.StartQueryLoopMetrics());
        }

        public async Task<IReadOnlyDictionary<string, CatalogLeafScan>> GetLeafScansAsync(string storageSuffix, string scanId, string pageId, IEnumerable<string> leafIds)
        {
            var sortedLeafIds = leafIds.OrderBy(x => x).ToList();
            if (sortedLeafIds.Count == 0)
            {
                return EmptyLeafIdToLeafScans;
            }
            else if (sortedLeafIds.Count == 1)
            {
                var leafScan = await GetLeafScanAsync(storageSuffix, scanId, pageId, sortedLeafIds[0]);
                if (leafScan == null)
                {
                    return EmptyLeafIdToLeafScans;
                }
                else
                {
                    return new Dictionary<string, CatalogLeafScan> { { leafScan.LeafId, leafScan } };
                }
            }

            var leafScans = await GetLeafScanTable(storageSuffix).GetEntitiesAsync<CatalogLeafScan>(
                CatalogLeafScan.GetPartitionKey(scanId, pageId),
                _telemetryClient.StartQueryLoopMetrics());
            var uniqueLeafIds = sortedLeafIds.ToHashSet();
            return leafScans
                .Where(x => uniqueLeafIds.Contains(x.LeafId))
                .ToDictionary(x => x.LeafId);
        }

        public async Task InsertAsync(IReadOnlyList<CatalogPageScan> pageScans)
        {
            foreach (var group in pageScans.GroupBy(x => x.StorageSuffix))
            {
                await GetPageScanTable(group.Key).InsertEntitiesAsync(group.ToList());
            }
        }

        public async Task InsertAsync(IReadOnlyList<CatalogLeafScan> leafScans)
        {
            foreach (var group in leafScans.GroupBy(x => x.StorageSuffix))
            {
                await GetLeafScanTable(group.Key).InsertEntitiesAsync(leafScans.ToList());
            }
        }

        public async Task<IReadOnlyList<CatalogIndexScan>> GetLatestIndexScans(string cursorName, int? maxEntities = 1000)
        {
            return await GetIndexScanTable().GetEntitiesAsync<CatalogIndexScan>(cursorName, _telemetryClient.StartQueryLoopMetrics(), maxEntities);
        }

        public async Task<CatalogIndexScan> GetIndexScanAsync(string cursorName, string scanId)
        {
            return await GetIndexScanTable().RetrieveAsync<CatalogIndexScan>(cursorName, scanId);
        }

        public async Task<CatalogPageScan> GetPageScanAsync(string storageSuffix, string scanId, string pageId)
        {
            return await GetPageScanTable(storageSuffix).RetrieveAsync<CatalogPageScan>(scanId, pageId);
        }

        public async Task<CatalogLeafScan> GetLeafScanAsync(string storageSuffix, string scanId, string pageId, string leafId)
        {
            return await GetLeafScanTable(storageSuffix).RetrieveAsync<CatalogLeafScan>(
                CatalogLeafScan.GetPartitionKey(scanId, pageId),
                leafId);
        }

        public async Task ReplaceAsync(CatalogIndexScan indexScan)
        {
            await GetIndexScanTable().ReplaceAsync(indexScan);
        }

        public async Task ReplaceAsync(CatalogPageScan pageScan)
        {
            await GetPageScanTable(pageScan.StorageSuffix).ReplaceAsync(pageScan);
        }

        public async Task ReplaceAsync(CatalogLeafScan leafScan)
        {
            await GetLeafScanTable(leafScan.StorageSuffix).ReplaceAsync(leafScan);
        }

        public async Task ReplaceAsync(IEnumerable<CatalogLeafScan> leafScans)
        {
            await BatchLeafScansAsync(leafScans, TableOperation.Replace);
        }

        public async Task DeleteAsync(IEnumerable<CatalogLeafScan> leafScans)
        {
            await BatchLeafScansAsync(leafScans, TableOperation.Delete);
        }

        private async Task BatchLeafScansAsync(IEnumerable<CatalogLeafScan> leafScans, Func<CatalogLeafScan, TableOperation> getOperation)
        {
            if (!leafScans.Any())
            {
                return;
            }

            var storageSuffixAndPartitionKeys = leafScans.Select(x => new { x.StorageSuffix, x.PartitionKey }).Distinct();
            if (storageSuffixAndPartitionKeys.Count() > 1)
            {
                throw new ArgumentException("All leaf scans must have the same storage suffix and partition key.");
            }

            var table = GetLeafScanTable(storageSuffixAndPartitionKeys.Single().StorageSuffix);

            var batch = new TableBatchOperation();
            foreach (var leafScan in leafScans)
            {
                batch.Add(getOperation(leafScan));
            }

            await table.ExecuteBatchAsync(batch);
        }

        public async Task<int> GetPageScanCountLowerBoundAsync(string storageSuffix, string scanId)
        {
            return await GetPageScanTable(storageSuffix).GetEntityCountLowerBoundAsync<CatalogPageScan>(
                scanId,
                _telemetryClient.StartQueryLoopMetrics());
        }

        public async Task<int> GetLeafScanCountLowerBoundAsync(string storageSuffix, string scanId)
        {
            return await GetLeafScanTable(storageSuffix).GetEntityCountLowerBoundAsync<CatalogLeafScan>(
                CatalogLeafScan.GetPartitionKey(scanId, string.Empty),
                CatalogLeafScan.GetPartitionKey(scanId, char.MaxValue.ToString()),
                _telemetryClient.StartQueryLoopMetrics());
        }

        public async Task DeleteAsync(CatalogIndexScan indexScan)
        {
            await GetIndexScanTable().DeleteAsync(indexScan);
        }

        public async Task DeleteAsync(CatalogPageScan pageScan)
        {
            await GetPageScanTable(pageScan.StorageSuffix).DeleteAsync(pageScan);
        }

        public async Task DeleteAsync(CatalogLeafScan leafScan)
        {
            await GetLeafScanTable(leafScan.StorageSuffix).DeleteAsync(leafScan);
        }

        private CloudTableClient GetClient()
        {
            return _serviceClientFactory
                .GetStorageAccount()
                .CreateCloudTableClient();
        }

        private CloudTable GetIndexScanTable()
        {
            return GetClient().GetTableReference(_options.Value.CatalogIndexScanTableName);
        }

        private CloudTable GetPageScanTable(string suffix)
        {
            return GetClient().GetTableReference($"{_options.Value.CatalogPageScanTableName}{suffix}");
        }

        public CloudTable GetLeafScanTable(string suffix)
        {
            return GetClient().GetTableReference($"{_options.Value.CatalogLeafScanTableName}{suffix}");
        }
    }
}

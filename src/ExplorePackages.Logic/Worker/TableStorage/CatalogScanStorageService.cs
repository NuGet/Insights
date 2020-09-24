using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;
using static Knapcode.ExplorePackages.Logic.Worker.TableStorageUtility;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class CatalogScanStorageService
    {
        private readonly ServiceClientFactory _serviceClientFactory;

        public CatalogScanStorageService(ServiceClientFactory serviceClientFactory)
        {
            _serviceClientFactory = serviceClientFactory;
        }

        public async Task InitializeAsync()
        {
            await GetIndexScanTable().CreateIfNotExistsAsync(retry: true);
            await GetPageScanTable().CreateIfNotExistsAsync(retry: true);
            await GetLeafScanTable().CreateIfNotExistsAsync(retry: true);
        }

        public async Task InsertAsync(CatalogIndexScan indexScan)
        {
            var table = GetIndexScanTable();
            await table.ExecuteAsync(TableOperation.Insert(indexScan));
        }

        public async Task<IReadOnlyList<CatalogPageScan>> GetPageScansAsync(string scanId)
        {
            return await GetScansAsync<CatalogPageScan>(GetPageScanTable(), scanId);
        }

        public async Task<IReadOnlyList<CatalogLeafScan>> GetLeafScansAsync(string scanId, string pageId)
        {
            return await GetScansAsync<CatalogLeafScan>(GetLeafScanTable(), CatalogLeafScan.GetPartitionKey(scanId, pageId));
        }

        public async Task InsertAsync(IReadOnlyList<CatalogPageScan> pageScans)
        {
            await InsertScansAsync(GetPageScanTable(), pageScans);
        }

        public async Task InsertAsync(IReadOnlyList<CatalogLeafScan> leafScans)
        {
            await InsertScansAsync(GetLeafScanTable(), leafScans);
        }

        public async Task<CatalogIndexScan> GetIndexScanAsync(string scanId)
        {
            return await RetrieveAsync<CatalogIndexScan>(GetIndexScanTable(), scanId, string.Empty);
        }

        public async Task<CatalogPageScan> GetPageScanAsync(string scanId, string pageId)
        {
            return await RetrieveAsync<CatalogPageScan>(GetPageScanTable(), scanId, pageId);
        }

        public async Task<CatalogLeafScan> GetLeafScanAsync(string scanId, string pageId, string leafId)
        {
            return await RetrieveAsync<CatalogLeafScan>(
                GetLeafScanTable(),
                CatalogLeafScan.GetPartitionKey(scanId, pageId),
                leafId);
        }

        public async Task ReplaceAsync(CatalogIndexScan indexScan)
        {
            await ReplaceAsync(GetIndexScanTable(), indexScan);
        }

        public async Task ReplaceAsync(CatalogPageScan pageScan)
        {
            await ReplaceAsync(GetPageScanTable(), pageScan);
        }

        public async Task<int> GetPageScanCountLowerBoundAsync(string scanId)
        {
            return await GetScanCountLowerBoundAsync<CatalogPageScan>(GetPageScanTable(), scanId);
        }

        public async Task<int> GetLeafScanCountLowerBoundAsync(string scanId, string pageId)
        {
            return await GetScanCountLowerBoundAsync<CatalogLeafScan>(GetLeafScanTable(), CatalogLeafScan.GetPartitionKey(scanId, pageId));
        }

        public async Task DeleteAsync(CatalogPageScan pageScan)
        {
            await DeleteAsync(GetPageScanTable(), pageScan);
        }

        public async Task DeleteAsync(CatalogLeafScan leafScan)
        {
            await DeleteAsync(GetLeafScanTable(), leafScan);
        }

        private async Task<IReadOnlyList<T>> GetScansAsync<T>(CloudTable table, string partitionKey) where T : ITableEntity, new()
        {
            var scans = new List<T>();
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
                var segment = await table.ExecuteQuerySegmentedAsync(query, token);
                token = segment.ContinuationToken;
                scans.AddRange(segment.Results);
            }
            while (token != null);

            return scans;
        }

        private async Task InsertScansAsync<T>(CloudTable table, IReadOnlyList<T> scans) where T : ITableEntity
        {
            if (!scans.Any())
            {
                return;
            }

            var batch = new TableBatchOperation();
            foreach (var scan in scans)
            {
                if (batch.Count >= MaxBatchSize)
                {
                    await table.ExecuteBatchAsync(batch);
                    batch = new TableBatchOperation();
                }

                batch.Add(TableOperation.Insert(scan));
            }

            if (batch.Count > 0)
            {
                await table.ExecuteBatchAsync(batch);
            }
        }

        private async Task<int> GetScanCountLowerBoundAsync<T>(CloudTable table, string partitionKey) where T : ITableEntity, new()
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

        private async Task<T> RetrieveAsync<T>(CloudTable table, string partitionKey, string rowKey) where T : class, ITableEntity
        {
            var result = await table.ExecuteAsync(TableOperation.Retrieve<T>(partitionKey, rowKey));
            return result.Result != null ? (T)result.Result : null;
        }

        private async Task ReplaceAsync<T>(CloudTable table, T scan) where T : ITableEntity
        {
            await table.ExecuteAsync(TableOperation.Replace(scan));
        }

        private async Task DeleteAsync<T>(CloudTable table, T scan) where T : ITableEntity
        {
            await table.ExecuteAsync(TableOperation.Delete(scan));
        }

        private CloudTableClient GetClient()
        {
            return _serviceClientFactory
                .GetLatestPackageLeavesStorageAccount()
                .CreateCloudTableClient();
        }

        private CloudTable GetIndexScanTable()
        {
            return GetClient().GetTableReference("catalogindexscans");
        }

        private CloudTable GetPageScanTable()
        {
            return GetClient().GetTableReference("catalogpagescans");
        }

        private CloudTable GetLeafScanTable()
        {
            return GetClient().GetTableReference("catalogleafscans");
        }
    }
}

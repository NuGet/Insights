using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.Worker
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
            return await GetPageScanTable(storageSuffix).GetEntitiesAsync<CatalogPageScan>(scanId);
        }

        public async Task<IReadOnlyList<CatalogLeafScan>> GetLeafScansAsync(string storageSuffix, string scanId, string pageId)
        {
            return await GetLeafScanTable(storageSuffix).GetEntitiesAsync<CatalogLeafScan>(CatalogLeafScan.GetPartitionKey(scanId, pageId));
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

        public async Task<CatalogIndexScan> GetIndexScanAsync(string scanId)
        {
            return await GetIndexScanTable().RetrieveAsync<CatalogIndexScan>(scanId, string.Empty);
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

        public async Task<int> GetPageScanCountLowerBoundAsync(string storageSuffix, string scanId)
        {
            return await GetPageScanTable(storageSuffix).GetEntityCountLowerBoundAsync<CatalogPageScan>(scanId);
        }

        public async Task<int> GetLeafScanCountLowerBoundAsync(string storageSuffix, string scanId, string pageId)
        {
            return await GetLeafScanTable(storageSuffix).GetEntityCountLowerBoundAsync<CatalogLeafScan>(CatalogLeafScan.GetPartitionKey(scanId, pageId));
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
            return GetClient().GetTableReference("catalogindexscans");
        }

        private CloudTable GetPageScanTable(string suffix)
        {
            return GetClient().GetTableReference($"catalogpagescans{suffix}");
        }

        private CloudTable GetLeafScanTable(string suffix)
        {
            return GetClient().GetTableReference($"catalogleafscans{suffix}");
        }
    }
}

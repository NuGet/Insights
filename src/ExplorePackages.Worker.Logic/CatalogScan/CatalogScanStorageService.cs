using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogScanStorageService
    {
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
            return await GetPageScanTable(storageSuffix).GetEntitiesAsync<CatalogPageScan>(scanId, _telemetryClient.NewQueryLoopMetrics());
        }

        public async Task<IReadOnlyList<CatalogLeafScan>> GetLeafScansAsync(string storageSuffix, string scanId, string pageId)
        {
            return await GetLeafScanTable(storageSuffix).GetEntitiesAsync<CatalogLeafScan>(
                CatalogLeafScan.GetPartitionKey(scanId, pageId),
                _telemetryClient.NewQueryLoopMetrics());
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
            return await GetIndexScanTable().GetEntitiesAsync<CatalogIndexScan>(cursorName, _telemetryClient.NewQueryLoopMetrics(), maxEntities);
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

        public async Task<int> GetPageScanCountLowerBoundAsync(string storageSuffix, string scanId)
        {
            return await GetPageScanTable(storageSuffix).GetEntityCountLowerBoundAsync<CatalogPageScan>(
                scanId,
                _telemetryClient.NewQueryLoopMetrics());
        }

        public async Task<int> GetLeafScanCountLowerBoundAsync(string storageSuffix, string scanId, string pageId)
        {
            return await GetLeafScanTable(storageSuffix).GetEntityCountLowerBoundAsync<CatalogLeafScan>(
                CatalogLeafScan.GetPartitionKey(scanId, pageId),
                _telemetryClient.NewQueryLoopMetrics());
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

        private CloudTable GetLeafScanTable(string suffix)
        {
            return GetClient().GetTableReference($"{_options.Value.CatalogLeafScanTableName}{suffix}");
        }
    }
}

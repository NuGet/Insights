using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;
using static Knapcode.ExplorePackages.Logic.Worker.TableStorageConstants;

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
            await GetIndexScanTable().CreateIfNotExistsAsync();
            await GetPageScanTable().CreateIfNotExistsAsync();
        }

        public async Task CreateIndexScanAsync(CatalogIndexScan catalogScan)
        {
            var table = GetIndexScanTable();
            await table.ExecuteAsync(TableOperation.Insert(catalogScan));
        }

        public async Task<CatalogIndexScan> GetIndexScanAsync(string scanId)
        {
            var table = GetIndexScanTable();
            var result = await table.ExecuteAsync(TableOperation.Retrieve<CatalogIndexScan>(scanId, string.Empty));
            return result.Result != null ? (CatalogIndexScan)result.Result : null;
        }

        public async Task UpdateIndexScanAsync(CatalogIndexScan catalogScan)
        {
            var table = GetIndexScanTable();
            await table.ExecuteAsync(TableOperation.Replace(catalogScan));
        }

        public async Task<int> GetPageScanCountLowerBoundAsync(string scanId)
        {
            var table = GetPageScanTable();
            var query = new TableQuery<CatalogPageScan>
            {
                FilterString = TableQuery.GenerateFilterCondition(
                    PartitionKey,
                    QueryComparisons.Equal,
                    scanId),
                TakeCount = MaxTakeCount,
                SelectColumns = Array.Empty<string>(),
            };

            TableContinuationToken token = null;
            do
            {
                var segment = await table.ExecuteQuerySegmentedAsync(query, token);
                token = segment.ContinuationToken;

                if (segment.Results.Count > 0)
                {
                    return segment.Results.Count;
                }
            }
            while (token != null);

            return 0;
        }

        public async Task<IReadOnlyList<CatalogPageScan>> GetPageScansAsync(string scanId)
        {
            var table = GetPageScanTable();

            var pages = new List<CatalogPageScan>();
            var query = new TableQuery<CatalogPageScan>
            {
                FilterString = TableQuery.GenerateFilterCondition(
                    PartitionKey,
                    QueryComparisons.Equal,
                    scanId),
                TakeCount = MaxTakeCount,
            };

            TableContinuationToken token = null;
            do
            {
                var segment = await table.ExecuteQuerySegmentedAsync(query, token);
                token = segment.ContinuationToken;
                pages.AddRange(segment.Results);
            }
            while (token != null);

            return pages;
        }

        public async Task AddPageScansAsync(IReadOnlyList<CatalogPageScan> pageScans)
        {
            if (!pageScans.Any())
            {
                return;
            }

            var table = GetPageScanTable();

            var batch = new TableBatchOperation();
            foreach (var pageScan in pageScans)
            {
                if (batch.Count >= MaxBatchSize)
                {
                    await table.ExecuteBatchAsync(batch);
                    batch = new TableBatchOperation();
                }

                batch.Add(TableOperation.Insert(pageScan));
            }

            if (batch.Count > 0)
            {
                await table.ExecuteBatchAsync(batch);
            }
        }

        public async Task<CatalogPageScan> GetPageScanAsync(string scanId, string pageId)
        {
            var table = GetPageScanTable();
            var result = await table.ExecuteAsync(TableOperation.Retrieve<CatalogPageScan>(scanId, pageId));
            return result.Result != null ? (CatalogPageScan)result.Result : null;
        }

        public async Task DeletePageScanAsync(CatalogPageScan pageScan)
        {
            var table = GetPageScanTable();
            await table.ExecuteAsync(TableOperation.Delete(pageScan));
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
    }
}

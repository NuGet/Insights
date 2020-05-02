using System;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class FindLatestLeavesCatalogScanDriver : ICatalogScanDriver
    {
        private readonly CatalogClient _catalogClient;
        private readonly LatestPackageLeafStorageService _storageService;

        public FindLatestLeavesCatalogScanDriver(
            CatalogClient catalogClient,
            LatestPackageLeafStorageService storageService)
        {
            _catalogClient = catalogClient;
            _storageService = storageService;
        }

        public Task<CatalogIndexScanResult> ProcessIndexAsync(CatalogIndexScan indexScan)
        {
            return Task.FromResult(CatalogIndexScanResult.Expand);
        }

        public async Task<CatalogPageScanResult> ProcessPageAsync(CatalogPageScan pageScan)
        {
            var page = await _catalogClient.GetCatalogPageAsync(pageScan.Url);
            var items = page.GetLeavesInBounds(pageScan.Min, pageScan.Max, excludeRedundantLeaves: true);
            await _storageService.AddAsync(pageScan.ScanId, items);
            return CatalogPageScanResult.Processed;
        }

        public Task ProcessLeafAsync(CatalogLeafScan leafScan)
        {
            throw new NotImplementedException();
        }
    }
}

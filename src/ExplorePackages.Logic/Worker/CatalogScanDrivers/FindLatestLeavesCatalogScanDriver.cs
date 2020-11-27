using System;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker
{
    public class FindLatestLeavesCatalogScanDriver : ICatalogScanDriver
    {
        private readonly CatalogClient _catalogClient;
        private readonly LatestPackageLeafStorageService _storageService;
        private readonly SchemaSerializer _schemaSerializer;

        public FindLatestLeavesCatalogScanDriver(
            CatalogClient catalogClient,
            LatestPackageLeafStorageService storageService,
            SchemaSerializer parameterSerializer)
        {
            _catalogClient = catalogClient;
            _storageService = storageService;
            _schemaSerializer = parameterSerializer;
        }

        public Task<CatalogIndexScanResult> ProcessIndexAsync(CatalogIndexScan indexScan)
        {
            return Task.FromResult(CatalogIndexScanResult.Expand);
        }

        public async Task<CatalogPageScanResult> ProcessPageAsync(CatalogPageScan pageScan)
        {
            var parameters = (FindLatestLeavesParameters)_schemaSerializer.Deserialize(pageScan.ScanParameters);
            var page = await _catalogClient.GetCatalogPageAsync(pageScan.Url);
            var items = page.GetLeavesInBounds(pageScan.Min, pageScan.Max, excludeRedundantLeaves: true);
            await _storageService.AddAsync(parameters.Prefix, items);
            return CatalogPageScanResult.Processed;
        }

        public Task ProcessLeafAsync(CatalogLeafScan leafScan) => throw new NotImplementedException();
        public Task StartAggregateAsync(CatalogIndexScan indexScan) => Task.CompletedTask;
        public Task<bool> IsAggregateCompleteAsync(CatalogIndexScan indexScan) => Task.FromResult(true);
        public Task FinalizeAsync(CatalogIndexScan indexScan) => Task.CompletedTask;
    }
}

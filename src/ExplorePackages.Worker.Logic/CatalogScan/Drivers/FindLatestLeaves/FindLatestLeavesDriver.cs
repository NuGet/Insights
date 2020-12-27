using System;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker.FindLatestLeaves
{
    public class FindLatestLeavesDriver : ICatalogScanDriver
    {
        private readonly CatalogClient _catalogClient;
        private readonly LatestPackageLeafStorageService _storageService;
        private readonly SchemaSerializer _schemaSerializer;

        public FindLatestLeavesDriver(
            CatalogClient catalogClient,
            LatestPackageLeafStorageService storageService,
            SchemaSerializer schemaSerializer)
        {
            _catalogClient = catalogClient;
            _storageService = storageService;
            _schemaSerializer = schemaSerializer;
        }

        public async Task<CatalogIndexScanResult> ProcessIndexAsync(CatalogIndexScan indexScan)
        {
            await _storageService.InitializeAsync();

            return CatalogIndexScanResult.Expand;
        }

        public async Task<CatalogPageScanResult> ProcessPageAsync(CatalogPageScan pageScan)
        {
            var parameters = (FindLatestLeavesParameters)_schemaSerializer.Deserialize(pageScan.ScanParameters);

            if (parameters.Prefix != string.Empty)
            {
                throw new NotImplementedException("The cursor does not contain the prefix or anything specific to the prefix so it must be an empty string.");
            }

            var page = await _catalogClient.GetCatalogPageAsync(pageScan.Url);
            var items = page.GetLeavesInBounds(pageScan.Min, pageScan.Max, excludeRedundantLeaves: true);
            await _storageService.AddAsync(parameters.Prefix, items);
            return CatalogPageScanResult.Processed;
        }

        public Task<DriverResult> ProcessLeafAsync(CatalogLeafScan leafScan) => throw new NotImplementedException();
        public Task StartAggregateAsync(CatalogIndexScan indexScan) => Task.CompletedTask;
        public Task<bool> IsAggregateCompleteAsync(CatalogIndexScan indexScan) => Task.FromResult(true);
        public Task FinalizeAsync(CatalogIndexScan indexScan) => Task.CompletedTask;
    }
}

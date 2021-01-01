using System;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker.FindLatestLeaves
{
    public class FindLatestLeavesDriver : ICatalogScanDriver
    {
        private readonly CatalogClient _catalogClient;
        private readonly LatestPackageLeafStorageService _storageService;
        private readonly SchemaSerializer _schemaSerializer;
        private readonly CatalogScanService _catalogScanService;

        public FindLatestLeavesDriver(
            CatalogClient catalogClient,
            LatestPackageLeafStorageService storageService,
            SchemaSerializer schemaSerializer,
            CatalogScanService catalogScanService)
        {
            _catalogClient = catalogClient;
            _storageService = storageService;
            _schemaSerializer = schemaSerializer;
            _catalogScanService = catalogScanService;
        }

        public async Task<CatalogIndexScanResult> ProcessIndexAsync(CatalogIndexScan indexScan)
        {
            var parameters = DeserializeParameters(indexScan.DriverParameters);
            if (parameters.StorageSuffix == string.Empty)
            {
                if (indexScan.CursorName != _catalogScanService.GetCursorName(CatalogScanDriverType.FindLatestLeaves))
                {
                    throw new NotSupportedException("When using the primary latest leaves table, only the main cursor name is allowed.");
                }
            }
            else
            {
                if (indexScan.CursorName != string.Empty)
                {
                    throw new NotSupportedException("When using the non-primary main latest leaves table, no cursor is allowed.");
                }
            }

            await _storageService.InitializeAsync(parameters.StorageSuffix);

            return CatalogIndexScanResult.ExpandAllLeaves;
        }

        public async Task<CatalogPageScanResult> ProcessPageAsync(CatalogPageScan pageScan)
        {
            var parameters = DeserializeParameters(pageScan.DriverParameters);
            var page = await _catalogClient.GetCatalogPageAsync(pageScan.Url);
            var leafItemToRank = page.GetLeafItemToRank();
            var items = page.GetLeavesInBounds(pageScan.Min, pageScan.Max, excludeRedundantLeaves: true);
            await _storageService.AddAsync(parameters.StorageSuffix, parameters.Prefix, items, leafItemToRank, pageScan.Rank, pageScan.Url);
            return CatalogPageScanResult.Processed;
        }

        private FindLatestLeavesParameters DeserializeParameters(string scanParameters)
        {
            var parameters = (FindLatestLeavesParameters)_schemaSerializer.Deserialize(scanParameters).Data;

            if (parameters.StorageSuffix == string.Empty)
            {
                if (parameters.Prefix != string.Empty)
                {
                    throw new NotSupportedException("When using the primary latest leaves table, only an empty prefix is allowed.");
                }
            }

            return parameters;
        }

        public Task<DriverResult> ProcessLeafAsync(CatalogLeafScan leafScan) => throw new NotImplementedException();
        public Task StartAggregateAsync(CatalogIndexScan indexScan) => Task.CompletedTask;
        public Task<bool> IsAggregateCompleteAsync(CatalogIndexScan indexScan) => Task.FromResult(true);
        public Task FinalizeAsync(CatalogIndexScan indexScan) => Task.CompletedTask;
    }
}

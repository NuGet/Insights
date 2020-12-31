using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Worker.FindLatestLeaves
{
    public class FindLatestLeavesDriver : ICatalogScanDriver
    {
        private readonly CatalogClient _catalogClient;
        private readonly LatestPackageLeafStorageService _storageService;
        private readonly SchemaSerializer _schemaSerializer;
        private readonly CatalogScanService _catalogScanService;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;

        public FindLatestLeavesDriver(
            CatalogClient catalogClient,
            LatestPackageLeafStorageService storageService,
            SchemaSerializer schemaSerializer,
            CatalogScanService catalogScanService,
            IOptions<ExplorePackagesWorkerSettings> options)
        {
            _catalogClient = catalogClient;
            _storageService = storageService;
            _schemaSerializer = schemaSerializer;
            _catalogScanService = catalogScanService;
            _options = options;
        }

        public async Task<CatalogIndexScanResult> ProcessIndexAsync(CatalogIndexScan indexScan)
        {
            var parameters = DeserializeParameters(indexScan.ScanParameters);
            if (parameters.TableName == _options.Value.LatestLeavesTableName)
            {
                if (indexScan.CursorName != _catalogScanService.GetCursorName(CatalogScanType.FindLatestLeaves))
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

            await _storageService.InitializeAsync(parameters.TableName);

            return CatalogIndexScanResult.Expand;
        }

        public async Task<CatalogPageScanResult> ProcessPageAsync(CatalogPageScan pageScan)
        {
            var parameters = DeserializeParameters(pageScan.ScanParameters);
            var page = await _catalogClient.GetCatalogPageAsync(pageScan.Url);
            var items = page.GetLeavesInBounds(pageScan.Min, pageScan.Max, excludeRedundantLeaves: true);
            await _storageService.AddAsync(parameters.TableName, parameters.Prefix, items, pageScan.Url);
            return CatalogPageScanResult.Processed;
        }

        private FindLatestLeavesParameters DeserializeParameters(string scanParameters)
        {
            var parameters = (FindLatestLeavesParameters)_schemaSerializer.Deserialize(scanParameters).Data;

            if (parameters.TableName == _options.Value.LatestLeavesTableName)
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

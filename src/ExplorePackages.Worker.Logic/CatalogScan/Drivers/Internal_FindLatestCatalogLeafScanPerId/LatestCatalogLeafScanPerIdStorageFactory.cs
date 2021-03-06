using System.Collections.Generic;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker.FindLatestCatalogLeafScanPerId
{
    public class LatestCatalogLeafScanPerIdStorageFactory : ILatestPackageLeafStorageFactory<CatalogLeafScanPerId>
    {
        private readonly SchemaSerializer _serializer;
        private readonly CatalogScanStorageService _storageService;

        public LatestCatalogLeafScanPerIdStorageFactory(
            SchemaSerializer serializer,
            CatalogScanStorageService storageService)
        {
            _serializer = serializer;
            _storageService = storageService;
        }

        public Task InitializeAsync(CatalogIndexScan indexScan)
        {
            return Task.CompletedTask;
        }

        public async Task<ILatestPackageLeafStorage<CatalogLeafScanPerId>> CreateAsync(CatalogPageScan pageScan, IReadOnlyDictionary<CatalogLeafItem, int> leafItemToRank)
        {
            var parameters = (CatalogIndexScanMessage)_serializer.Deserialize(pageScan.DriverParameters).Data;
            var indexScan = await _storageService.GetIndexScanAsync(parameters.CursorName, parameters.ScanId);
            var table = _storageService.GetLeafScanTable(indexScan.StorageSuffix);
            return new LatestCatalogLeafScanPerIdStorage(table, indexScan);
        }
    }
}

using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogLeafScanToCsvAdapter<T> : BaseCatalogLeafScanToCsvAdapter, ICatalogLeafScanNonBatchDriver where T : class, ICsvRecord
    {
        private readonly ICatalogLeafToCsvDriver<T> _driver;

        public CatalogLeafScanToCsvAdapter(
            SchemaSerializer schemaSerializer,
            CsvTemporaryStorageFactory intermediateStorageFactory,
            ICsvResultStorage<T> resultStorage,
            ICatalogLeafToCsvDriver<T> driver) : base(
                schemaSerializer,
                intermediateStorageFactory,
                intermediateStorageFactory.Create(resultStorage),
                driver)
        {
            _driver = driver;
        }

        public async Task<DriverResult> ProcessLeafAsync(CatalogLeafScan leafScan)
        {
            var leafItem = leafScan.ToLeafItem();
            var result = await _driver.ProcessLeafAsync(leafItem, leafScan.AttemptCount);
            if (result.Type == DriverResultType.TryAgainLater)
            {
                return result;
            }

            await _storage[0].AppendAsync(leafScan.StorageSuffix, result.Value);

            return result;
        }
    }

    public class CatalogLeafScanToCsvAdapter<T1, T2> : BaseCatalogLeafScanToCsvAdapter, ICatalogLeafScanNonBatchDriver
        where T1 : class, ICsvRecord
        where T2 : class, ICsvRecord
    {
        private readonly ICatalogLeafToCsvDriver<T1, T2> _driver;

        public CatalogLeafScanToCsvAdapter(
            SchemaSerializer schemaSerializer,
            CsvTemporaryStorageFactory intermediateStorageFactory,
            ICsvResultStorage<T1> resultStorage1,
            ICsvResultStorage<T2> resultStorage2,
            ICatalogLeafToCsvDriver<T1, T2> driver) : base(
                schemaSerializer,
                intermediateStorageFactory,
                intermediateStorageFactory.Create(resultStorage1, resultStorage2),
                driver)
        {
            _driver = driver;
        }

        public async Task<DriverResult> ProcessLeafAsync(CatalogLeafScan leafScan)
        {
            var leafItem = leafScan.ToLeafItem();
            var result = await _driver.ProcessLeafAsync(leafItem, leafScan.AttemptCount);
            if (result.Type == DriverResultType.TryAgainLater)
            {
                return result;
            }

            await _storage[0].AppendAsync(leafScan.StorageSuffix, result.Value.Item1);
            await _storage[1].AppendAsync(leafScan.StorageSuffix, result.Value.Item2);

            return result;
        }
    }
}

using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Insights.Worker
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

        protected override async Task<(DriverResult, IReadOnlyList<ICsvRecordSet<ICsvRecord>>)> ProcessLeafAsync(CatalogLeafItem item, int attemptCount)
        {
            var result = await _driver.ProcessLeafAsync(item, attemptCount);
            return (result, (CsvRecordSets<T>)GetValueOrDefault(result));
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

        protected override async Task<(DriverResult, IReadOnlyList<ICsvRecordSet<ICsvRecord>>)> ProcessLeafAsync(CatalogLeafItem item, int attemptCount)
        {
            var result = await _driver.ProcessLeafAsync(item, attemptCount);
            return (result, GetValueOrDefault(result));
        }
    }
}

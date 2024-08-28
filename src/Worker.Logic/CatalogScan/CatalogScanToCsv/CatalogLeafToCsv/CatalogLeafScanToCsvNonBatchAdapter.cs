// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public class CatalogLeafScanToCsvNonBatchAdapter<T> : BaseCatalogLeafScanToCsvNonBatchAdapter, ICatalogLeafScanNonBatchDriver where T : class, IAggregatedCsvRecord<T>
    {
        private readonly ICatalogLeafToCsvDriver<T> _driver;

        public CatalogLeafScanToCsvNonBatchAdapter(
            CsvTemporaryStorageFactory intermediateStorageFactory,
            ICsvResultStorage<T> resultStorage,
            ICatalogLeafToCsvDriver<T> driver,
            ServiceClientFactory serviceClientFactory) : base(
                intermediateStorageFactory,
                intermediateStorageFactory.Create(resultStorage),
                driver,
                serviceClientFactory,
                [resultStorage.ResultContainerName])
        {
            _driver = driver;
        }

        protected override async Task<(DriverResult, IReadOnlyList<IReadOnlyList<IAggregatedCsvRecord>>)> ProcessLeafInternalAsync(CatalogLeafScan leafScan)
        {
            var result = await _driver.ProcessLeafAsync(leafScan);
            if (result.Type == DriverResultType.Success)
            {
                return (result, [result.Value]);
            }
            else
            {
                return (result, default);
            }
        }
    }

    public class CatalogLeafScanToCsvNonBatchAdapter<T1, T2> : BaseCatalogLeafScanToCsvNonBatchAdapter, ICatalogLeafScanNonBatchDriver
        where T1 : class, IAggregatedCsvRecord<T1>
        where T2 : class, IAggregatedCsvRecord<T2>
    {
        private readonly ICatalogLeafToCsvDriver<T1, T2> _driver;

        public CatalogLeafScanToCsvNonBatchAdapter(
            CsvTemporaryStorageFactory intermediateStorageFactory,
            ICsvResultStorage<T1> resultStorage1,
            ICsvResultStorage<T2> resultStorage2,
            ICatalogLeafToCsvDriver<T1, T2> driver,
            ServiceClientFactory serviceClientFactory) : base(
                intermediateStorageFactory,
                intermediateStorageFactory.Create(resultStorage1, resultStorage2),
                driver,
                serviceClientFactory,
                [resultStorage1.ResultContainerName, resultStorage2.ResultContainerName])
        {
            _driver = driver;
        }

        protected override async Task<(DriverResult, IReadOnlyList<IReadOnlyList<IAggregatedCsvRecord>>)> ProcessLeafInternalAsync(CatalogLeafScan leafScan)
        {
            var result = await _driver.ProcessLeafAsync(leafScan);
            if (result.Type == DriverResultType.Success)
            {
                return (result, [result.Value.Records1, result.Value.Records2]);
            }
            else
            {
                return (result, default);
            }
        }
    }

    public class CatalogLeafScanToCsvNonBatchAdapter<T1, T2, T3> : BaseCatalogLeafScanToCsvNonBatchAdapter, ICatalogLeafScanNonBatchDriver
        where T1 : class, IAggregatedCsvRecord<T1>
        where T2 : class, IAggregatedCsvRecord<T2>
        where T3 : class, IAggregatedCsvRecord<T3>
    {
        private readonly ICatalogLeafToCsvDriver<T1, T2, T3> _driver;

        public CatalogLeafScanToCsvNonBatchAdapter(
            CsvTemporaryStorageFactory intermediateStorageFactory,
            ICsvResultStorage<T1> resultStorage1,
            ICsvResultStorage<T2> resultStorage2,
            ICsvResultStorage<T3> resultStorage3,
            ICatalogLeafToCsvDriver<T1, T2, T3> driver,
            ServiceClientFactory serviceClientFactory) : base(
                intermediateStorageFactory,
                intermediateStorageFactory.Create(resultStorage1, resultStorage2, resultStorage3),
                driver,
                serviceClientFactory,
                [resultStorage1.ResultContainerName, resultStorage2.ResultContainerName, resultStorage3.ResultContainerName])
        {
            _driver = driver;
        }

        protected override async Task<(DriverResult, IReadOnlyList<IReadOnlyList<IAggregatedCsvRecord>>)> ProcessLeafInternalAsync(CatalogLeafScan leafScan)
        {
            var result = await _driver.ProcessLeafAsync(leafScan);
            if (result.Type == DriverResultType.Success)
            {
                return (result, [result.Value.Records1, result.Value.Records2, result.Value.Records3]);
            }
            else
            {
                return (result, default);
            }
        }
    }
}

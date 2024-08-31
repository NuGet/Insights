// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public class CatalogLeafScanToCsvBatchAdapter<T> : BaseCatalogLeafScanToCsvBatchAdapter, ICatalogLeafScanBatchDriver
        where T : class, IAggregatedCsvRecord<T>
    {
        private readonly ICatalogLeafToCsvBatchDriver<T> _driver;

        public CatalogLeafScanToCsvBatchAdapter(
            CsvTemporaryStorageFactory intermediateStorageFactory,
            ICsvResultStorage<T> resultStorage,
            ICatalogLeafToCsvBatchDriver<T> driver,
            ServiceClientFactory serviceClientFactory) : base(
                intermediateStorageFactory,
                intermediateStorageFactory.Create(resultStorage),
                driver,
                serviceClientFactory,
                [resultStorage.ResultContainerName])
        {
            _driver = driver;
        }

        protected override async Task<BatchMessageProcessorResult<IReadOnlyList<IReadOnlyList<IAggregatedCsvRecord>>, CatalogLeafScan>> ProcessLeavesInternalAsync(IReadOnlyList<CatalogLeafScan> leafScans)
        {
            var result = await _driver.ProcessLeavesAsync(leafScans);
            return new BatchMessageProcessorResult<IReadOnlyList<IReadOnlyList<IAggregatedCsvRecord>>, CatalogLeafScan>(
                [result.Result],
                result.Failed,
                result.TryAgainLater);
        }
    }

    public class CatalogLeafScanToCsvBatchAdapter<T1, T2> : BaseCatalogLeafScanToCsvBatchAdapter, ICatalogLeafScanBatchDriver
        where T1 : class, IAggregatedCsvRecord<T1>
        where T2 : class, IAggregatedCsvRecord<T2>
    {
        private readonly ICatalogLeafToCsvBatchDriver<T1, T2> _driver;

        public CatalogLeafScanToCsvBatchAdapter(
            CsvTemporaryStorageFactory intermediateStorageFactory,
            ICsvResultStorage<T1> resultStorage1,
            ICsvResultStorage<T2> resultStorage2,
            ICatalogLeafToCsvBatchDriver<T1, T2> driver,
            ServiceClientFactory serviceClientFactory) : base(
                intermediateStorageFactory,
                intermediateStorageFactory.Create(resultStorage1, resultStorage2),
                driver,
                serviceClientFactory,
                [resultStorage1.ResultContainerName, resultStorage2.ResultContainerName])
        {
            _driver = driver;
        }

        protected override async Task<BatchMessageProcessorResult<IReadOnlyList<IReadOnlyList<IAggregatedCsvRecord>>, CatalogLeafScan>> ProcessLeavesInternalAsync(IReadOnlyList<CatalogLeafScan> leafScans)
        {
            var result = await _driver.ProcessLeavesAsync(leafScans);
            return new BatchMessageProcessorResult<IReadOnlyList<IReadOnlyList<IAggregatedCsvRecord>>, CatalogLeafScan>(
                result.Result,
                result.Failed,
                result.TryAgainLater);
        }
    }

    public class CatalogLeafScanToCsvBatchAdapter<T1, T2, T3> : BaseCatalogLeafScanToCsvBatchAdapter, ICatalogLeafScanBatchDriver
        where T1 : class, IAggregatedCsvRecord<T1>
        where T2 : class, IAggregatedCsvRecord<T2>
        where T3 : class, IAggregatedCsvRecord<T3>
    {
        private readonly ICatalogLeafToCsvBatchDriver<T1, T2, T3> _driver;

        public CatalogLeafScanToCsvBatchAdapter(
            CsvTemporaryStorageFactory intermediateStorageFactory,
            ICsvResultStorage<T1> resultStorage1,
            ICsvResultStorage<T2> resultStorage2,
            ICsvResultStorage<T3> resultStorage3,
            ICatalogLeafToCsvBatchDriver<T1, T2, T3> driver,
            ServiceClientFactory serviceClientFactory) : base(
                intermediateStorageFactory,
                intermediateStorageFactory.Create(resultStorage1, resultStorage2, resultStorage3),
                driver,
                serviceClientFactory,
                [resultStorage1.ResultContainerName, resultStorage2.ResultContainerName, resultStorage3.ResultContainerName])
        {
            _driver = driver;
        }

        protected override async Task<BatchMessageProcessorResult<IReadOnlyList<IReadOnlyList<IAggregatedCsvRecord>>, CatalogLeafScan>> ProcessLeavesInternalAsync(IReadOnlyList<CatalogLeafScan> leafScans)
        {
            var result = await _driver.ProcessLeavesAsync(leafScans);
            return new BatchMessageProcessorResult<IReadOnlyList<IReadOnlyList<IAggregatedCsvRecord>>, CatalogLeafScan>(
                result.Result,
                result.Failed,
                result.TryAgainLater);
        }
    }
}

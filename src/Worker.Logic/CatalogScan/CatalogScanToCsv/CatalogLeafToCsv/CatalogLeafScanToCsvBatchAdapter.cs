// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Insights.Worker
{
    public class CatalogLeafScanToCsvBatchAdapter<T> : BaseCatalogLeafScanToCsvBatchAdapter, ICatalogLeafScanBatchDriver where T : class, ICsvRecord
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
                new[] { resultStorage.ResultContainerName })
        {
            _driver = driver;
        }

        protected override async Task<BatchMessageProcessorResult<IReadOnlyList<IReadOnlyList<ICsvRecordSet<ICsvRecord>>>, CatalogLeafScan>> ProcessLeafAsync(IReadOnlyList<CatalogLeafScan> leafScans)
        {
            var result = await _driver.ProcessLeavesAsync(leafScans);
            return new BatchMessageProcessorResult<IReadOnlyList<IReadOnlyList<ICsvRecordSet<ICsvRecord>>>, CatalogLeafScan>(
                result.Result,
                result.Failed,
                result.TryAgainLater);
        }
    }

    public class CatalogLeafScanToCsvBatchAdapter<T1, T2> : BaseCatalogLeafScanToCsvBatchAdapter, ICatalogLeafScanBatchDriver
        where T1 : class, ICsvRecord
        where T2 : class, ICsvRecord
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
                new[] { resultStorage1.ResultContainerName, resultStorage2.ResultContainerName })
        {
            _driver = driver;
        }

        protected override async Task<BatchMessageProcessorResult<IReadOnlyList<IReadOnlyList<ICsvRecordSet<ICsvRecord>>>, CatalogLeafScan>> ProcessLeafAsync(IReadOnlyList<CatalogLeafScan> leafScans)
        {
            var result = await _driver.ProcessLeavesAsync(leafScans);
            return new BatchMessageProcessorResult<IReadOnlyList<IReadOnlyList<ICsvRecordSet<ICsvRecord>>>, CatalogLeafScan>(
                result.Result,
                result.Failed,
                result.TryAgainLater);
        }
    }

    public class CatalogLeafScanToCsvBatchAdapter<T1, T2, T3> : BaseCatalogLeafScanToCsvBatchAdapter, ICatalogLeafScanBatchDriver
        where T1 : class, ICsvRecord
        where T2 : class, ICsvRecord
        where T3 : class, ICsvRecord
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
                new[] { resultStorage1.ResultContainerName, resultStorage2.ResultContainerName, resultStorage3.ResultContainerName })
        {
            _driver = driver;
        }

        protected override async Task<BatchMessageProcessorResult<IReadOnlyList<IReadOnlyList<ICsvRecordSet<ICsvRecord>>>, CatalogLeafScan>> ProcessLeafAsync(IReadOnlyList<CatalogLeafScan> leafScans)
        {
            var result = await _driver.ProcessLeavesAsync(leafScans);
            return new BatchMessageProcessorResult<IReadOnlyList<IReadOnlyList<ICsvRecordSet<ICsvRecord>>>, CatalogLeafScan>(
                result.Result,
                result.Failed,
                result.TryAgainLater);
        }
    }
}

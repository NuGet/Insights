// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public abstract class BaseCatalogLeafScanToCsvBatchAdapter : BaseCatalogLeafScanToCsvAdapter
    {
        public BaseCatalogLeafScanToCsvBatchAdapter(
            CsvTemporaryStorageFactory storageFactory,
            IReadOnlyList<ICsvTemporaryStorage> storage,
            ICatalogLeafToCsvDriver driver,
            ServiceClientFactory serviceClientFactory,
            IReadOnlyList<string> resultContainerNames,
            IOptions<NuGetInsightsWorkerSettings> options)
            : base(storageFactory, storage, driver, serviceClientFactory, resultContainerNames, options)
        {
        }

        protected abstract Task<BatchMessageProcessorResult<IReadOnlyList<IReadOnlyList<IAggregatedCsvRecord>>, CatalogLeafScan>>
            ProcessLeavesInternalAsync(IReadOnlyList<CatalogLeafScan> leafScans);

        public async Task<BatchMessageProcessorResult<CatalogLeafScan>> ProcessLeavesAsync(IReadOnlyList<CatalogLeafScan> leafScans)
        {
            var batchResult = await ProcessLeavesInternalAsync(leafScans);

            var storageSuffix = leafScans
                .Select(x => x.StorageSuffix)
                .Distinct()
                .Single();

            for (var setIndex = 0; setIndex < _storage.Count; setIndex++)
            {
                await _storage[setIndex].AppendAsync(storageSuffix, batchResult.Result[setIndex]);
            }

            return batchResult;
        }
    }
}

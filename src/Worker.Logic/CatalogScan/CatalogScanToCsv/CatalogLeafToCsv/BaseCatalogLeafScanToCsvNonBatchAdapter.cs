// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public abstract class BaseCatalogLeafScanToCsvNonBatchAdapter : BaseCatalogLeafScanToCsvAdapter
    {
        private readonly ILogger _logger;

        public BaseCatalogLeafScanToCsvNonBatchAdapter(
            CsvTemporaryStorageFactory storageFactory,
            IReadOnlyList<ICsvTemporaryStorage> storage,
            ICatalogLeafToCsvDriver driver,
            ServiceClientFactory serviceClientFactory,
            IReadOnlyList<string> resultContainerNames,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger logger)
            : base(storageFactory, storage, driver, serviceClientFactory, resultContainerNames, options)
        {
            _logger = logger;
        }

        protected abstract Task<(DriverResult, IReadOnlyList<IReadOnlyList<IAggregatedCsvRecord>>)> ProcessLeafInternalAsync(CatalogLeafScan leafScan);

        public async Task<BatchMessageProcessorResult<CatalogLeafScan>> ProcessLeavesAsync(IReadOnlyList<CatalogLeafScan> leafScans)
        {
            var storageSuffix = leafScans
                .Select(x => x.StorageSuffix)
                .Distinct()
                .Single();

            var allSets = new List<List<IAggregatedCsvRecord>>();
            for (var setIndex = 0; setIndex < _storage.Count; setIndex++)
            {
                allSets.Add([]);
            }

            var batchResult = await CatalogLeafScanBatchDriverAdapter.ProcessLeavesOneByOneAsync(
                leafScans,
                ProcessLeafInternalAsync,
                sets =>
                {
                    for (var setIndex = 0; setIndex < _storage.Count; setIndex++)
                    {
                        allSets[setIndex].AddRange(sets[setIndex]);
                    }
                },
                _logger);

            for (var setIndex = 0; setIndex < _storage.Count; setIndex++)
            {
                await _storage[setIndex].AppendAsync(storageSuffix, allSets[setIndex]);
            }

            return batchResult;
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public abstract class BaseCatalogLeafScanToCsvNonBatchAdapter : BaseCatalogLeafScanToCsvAdapter
    {
        public BaseCatalogLeafScanToCsvNonBatchAdapter(
            CsvTemporaryStorageFactory storageFactory,
            IReadOnlyList<ICsvTemporaryStorage> storage,
            ICatalogLeafToCsvDriver driver,
            ServiceClientFactory serviceClientFactory,
            IReadOnlyList<string> resultContainerNames)
            : base(storageFactory, storage, driver, serviceClientFactory, resultContainerNames)
        {
        }

        protected abstract Task<(DriverResult, IReadOnlyList<ICsvRecordSet<ICsvRecord>>)> ProcessLeafInternalAsync(CatalogLeafScan leafScan);

        public async Task<DriverResult> ProcessLeafAsync(CatalogLeafScan leafScan)
        {
            (var result, var sets) = await ProcessLeafInternalAsync(leafScan);
            if (result.Type == DriverResultType.TryAgainLater)
            {
                return result;
            }

            for (var setIndex = 0; setIndex < _storage.Count; setIndex++)
            {
                await _storage[setIndex].AppendAsync(leafScan.StorageSuffix, new[] { sets[setIndex] });
            }

            return result;
        }
    }
}

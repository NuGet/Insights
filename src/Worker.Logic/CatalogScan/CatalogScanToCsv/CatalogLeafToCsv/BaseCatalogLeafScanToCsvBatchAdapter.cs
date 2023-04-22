// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuGet.Insights.Worker
{
    public abstract class BaseCatalogLeafScanToCsvBatchAdapter : BaseCatalogLeafScanToCsvAdapter
    {
        public BaseCatalogLeafScanToCsvBatchAdapter(
            SchemaSerializer schemaSerializer,
            CsvTemporaryStorageFactory storageFactory,
            IReadOnlyList<ICsvTemporaryStorage> storage,
            ICatalogLeafToCsvDriver driver,
            ServiceClientFactory serviceClientFactory,
            IReadOnlyList<string> resultContainerNames)
            : base(schemaSerializer, storageFactory, storage, driver, serviceClientFactory, resultContainerNames)
        {
        }

        protected abstract Task<BatchMessageProcessorResult<IReadOnlyList<IReadOnlyList<ICsvRecordSet<ICsvRecord>>>, CatalogLeafScan>>
            ProcessLeafAsync(IReadOnlyList<CatalogLeafScan> leafScans);

        public async Task<BatchMessageProcessorResult<CatalogLeafScan>> ProcessLeavesAsync(IReadOnlyList<CatalogLeafScan> leafScans)
        {
            var batchResult = await ProcessLeafAsync(leafScans);

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

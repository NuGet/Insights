// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Insights.Worker
{
    public class CatalogLeafScanToCsvNonBatchAdapter<T> : BaseCatalogLeafScanToCsvNonBatchAdapter, ICatalogLeafScanNonBatchDriver where T : class, ICsvRecord
    {
        private readonly ICatalogLeafToCsvDriver<T> _driver;

        public CatalogLeafScanToCsvNonBatchAdapter(
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

        protected override async Task<(DriverResult, IReadOnlyList<ICsvRecordSet<ICsvRecord>>)> ProcessLeafAsync(ICatalogLeafItem item, int attemptCount)
        {
            var result = await _driver.ProcessLeafAsync(item, attemptCount);
            if (result.Type == DriverResultType.Success)
            {
                return (result, new[] { result.Value });
            }
            else
            {
                return (result, default);
            }
        }
    }

    public class CatalogLeafScanToCsvNonBatchAdapter<T1, T2> : BaseCatalogLeafScanToCsvNonBatchAdapter, ICatalogLeafScanNonBatchDriver
        where T1 : class, ICsvRecord
        where T2 : class, ICsvRecord
    {
        private readonly ICatalogLeafToCsvDriver<T1, T2> _driver;

        public CatalogLeafScanToCsvNonBatchAdapter(
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

        protected override async Task<(DriverResult, IReadOnlyList<ICsvRecordSet<ICsvRecord>>)> ProcessLeafAsync(ICatalogLeafItem item, int attemptCount)
        {
            var result = await _driver.ProcessLeafAsync(item, attemptCount);
            if (result.Type == DriverResultType.Success)
            {
                return (result, new ICsvRecordSet<ICsvRecord>[] { result.Value.Sets1[0], result.Value.Sets2[0] });
            }
            else
            {
                return (result, default);
            }
        }
    }
}

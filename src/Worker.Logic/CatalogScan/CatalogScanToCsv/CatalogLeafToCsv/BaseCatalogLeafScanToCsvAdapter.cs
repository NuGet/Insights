// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Insights.Worker
{
    public abstract class BaseCatalogLeafScanToCsvAdapter
    {
        private readonly SchemaSerializer _schemaSerializer;
        private readonly CsvTemporaryStorageFactory _storageFactory;
        protected readonly IReadOnlyList<ICsvTemporaryStorage> _storage;
        private readonly ICatalogLeafToCsvDriver _driver;

        public BaseCatalogLeafScanToCsvAdapter(
            SchemaSerializer schemaSerializer,
            CsvTemporaryStorageFactory storageFactory,
            IReadOnlyList<ICsvTemporaryStorage> storage,
            ICatalogLeafToCsvDriver driver)
        {
            _schemaSerializer = schemaSerializer;
            _storageFactory = storageFactory;
            _storage = storage;
            _driver = driver;
        }

        public async Task InitializeAsync(CatalogIndexScan indexScan)
        {
            foreach (var storage in _storage)
            {
                await storage.InitializeAsync(indexScan.StorageSuffix);
            }
            await _storageFactory.InitializeAsync(indexScan.StorageSuffix);
            await _driver.InitializeAsync();
        }

        public async Task StartCustomExpandAsync(CatalogIndexScan indexScan)
        {
            foreach (var storage in _storage)
            {
                await storage.StartCustomExpandAsync(indexScan);
            }
        }

        public async Task<bool> IsCustomExpandCompleteAsync(CatalogIndexScan indexScan)
        {
            foreach (var storage in _storage)
            {
                if (!await storage.IsCustomExpandCompleteAsync(indexScan))
                {
                    return false;
                }
            }

            return true;
        }

        public Task<CatalogIndexScanResult> ProcessIndexAsync(CatalogIndexScan indexScan)
        {
            var parameters = DeserializeParameters(indexScan.DriverParameters);

            CatalogIndexScanResult result;
            switch (parameters.Mode)
            {
                case CatalogLeafToCsvMode.AllLeaves:
                    result = CatalogIndexScanResult.ExpandAllLeaves;
                    break;
                case CatalogLeafToCsvMode.LatestLeaves:
                    result = _driver.SingleMessagePerId ? CatalogIndexScanResult.ExpandLatestLeavesPerId : CatalogIndexScanResult.ExpandLatestLeaves;
                    break;
                case CatalogLeafToCsvMode.Reprocess:
                    result = CatalogIndexScanResult.CustomExpand;
                    break;
                default:
                    throw new NotImplementedException();
            }

            return Task.FromResult(result);
        }

        public Task<CatalogPageScanResult> ProcessPageAsync(CatalogPageScan pageScan)
        {
            var parameters = DeserializeParameters(pageScan.DriverParameters);
            if (parameters.Mode == CatalogLeafToCsvMode.AllLeaves)
            {
                return Task.FromResult(CatalogPageScanResult.ExpandAllowDuplicates);
            }

            throw new NotSupportedException();
        }

        protected CatalogLeafToCsvParameters DeserializeParameters(string driverParameters)
        {
            return (CatalogLeafToCsvParameters)_schemaSerializer.Deserialize(driverParameters).Data;
        }

        public async Task StartAggregateAsync(CatalogIndexScan indexScan)
        {
            foreach (var storage in _storage)
            {
                await storage.StartAggregateAsync(indexScan.GetScanId(), indexScan.StorageSuffix);
            }
        }

        public async Task<bool> IsAggregateCompleteAsync(CatalogIndexScan indexScan)
        {
            foreach (var storage in _storage)
            {
                if (!await storage.IsAggregateCompleteAsync(indexScan.GetScanId(), indexScan.StorageSuffix))
                {
                    return false;
                }
            }

            return true;
        }

        public async Task FinalizeAsync(CatalogIndexScan indexScan)
        {
            await _storageFactory.FinalizeAsync(indexScan.StorageSuffix);
            foreach (var storage in _storage)
            {
                await storage.FinalizeAsync(indexScan.StorageSuffix);
            }
        }
    }
}

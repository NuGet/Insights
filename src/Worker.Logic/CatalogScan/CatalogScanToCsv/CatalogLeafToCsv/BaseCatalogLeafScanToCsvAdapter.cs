// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public abstract class BaseCatalogLeafScanToCsvAdapter
    {
        private readonly CsvTemporaryStorageFactory _storageFactory;
        protected readonly IReadOnlyList<ICsvTemporaryStorage> _storage;
        private readonly ICatalogLeafToCsvDriver _driver;
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IReadOnlyList<string> _resultContainerNames;

        public BaseCatalogLeafScanToCsvAdapter(
            CsvTemporaryStorageFactory storageFactory,
            IReadOnlyList<ICsvTemporaryStorage> storage,
            ICatalogLeafToCsvDriver driver,
            ServiceClientFactory serviceClientFactory,
            IReadOnlyList<string> resultContainerNames)
        {
            _storageFactory = storageFactory;
            _storage = storage;
            _driver = driver;
            _serviceClientFactory = serviceClientFactory;
            _resultContainerNames = resultContainerNames;
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

        public Task<CatalogIndexScanResult> ProcessIndexAsync(CatalogIndexScan indexScan)
        {
            if (_driver.SingleMessagePerId)
            {
                if (!indexScan.OnlyLatestLeaves)
                {
                    throw new NotSupportedException($"If a single message per ID is desired, {nameof(CatalogIndexScan.OnlyLatestLeaves)} must be true.");
                }

                return Task.FromResult(CatalogIndexScanResult.ExpandLatestLeavesPerId);
            }
            else if (indexScan.OnlyLatestLeaves)
            {
                return Task.FromResult(CatalogIndexScanResult.ExpandLatestLeaves);
            }
            else
            {
                return Task.FromResult(CatalogIndexScanResult.ExpandAllLeaves);
            }
        }

        public Task<CatalogPageScanResult> ProcessPageAsync(CatalogPageScan pageScan)
        {
            if (pageScan.OnlyLatestLeaves)
            {
                throw new NotSupportedException($"To process catalog pages, {nameof(CatalogIndexScan.OnlyLatestLeaves)} must be false.");
            }

            return Task.FromResult(CatalogPageScanResult.ExpandAllowDuplicates);
        }

        public async Task StartAggregateAsync(CatalogIndexScan indexScan)
        {
            foreach (var storage in _storage)
            {
                await storage.StartAggregateAsync(indexScan.ScanId, indexScan.StorageSuffix);
            }
        }

        public async Task<bool> IsAggregateCompleteAsync(CatalogIndexScan indexScan)
        {
            foreach (var storage in _storage)
            {
                if (!await storage.IsAggregateCompleteAsync(indexScan.ScanId, indexScan.StorageSuffix))
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

        public async Task DestroyOutputAsync()
        {
            var serviceClient = await _serviceClientFactory.GetBlobServiceClientAsync();
            foreach (var container in _resultContainerNames)
            {
                var containerClient = serviceClient.GetBlobContainerClient(container);
                await containerClient.DeleteIfExistsAsync();
            }

            await _driver.DestroyAsync();
        }
    }
}

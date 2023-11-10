// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Insights.Worker
{
    public class FindLatestLeafDriver<T> : ICatalogLeafScanNonBatchDriver where T : class, ILatestPackageLeaf, new()
    {
        private readonly CatalogClient _catalogClient;
        private readonly ILatestPackageLeafStorageFactory<T> _storageFactory;
        private readonly LatestLeafStorageService<T> _storageService;
        private readonly ILogger<FindLatestLeafDriver<T>> _logger;

        public FindLatestLeafDriver(
            CatalogClient catalogClient,
            ILatestPackageLeafStorageFactory<T> storageFactory,
            LatestLeafStorageService<T> storageService,
            ILogger<FindLatestLeafDriver<T>> logger)
        {
            _catalogClient = catalogClient;
            _storageFactory = storageFactory;
            _storageService = storageService;
            _logger = logger;
        }

        public async Task InitializeAsync(CatalogIndexScan indexScan)
        {
            await _storageFactory.InitializeAsync();
        }

        public Task<CatalogIndexScanResult> ProcessIndexAsync(CatalogIndexScan indexScan)
        {
            return Task.FromResult(CatalogIndexScanResult.ExpandAllLeaves);
        }

        public async Task<CatalogPageScanResult> ProcessPageAsync(CatalogPageScan pageScan)
        {
            var page = await _catalogClient.GetCatalogPageAsync(pageScan.Url);
            var items = page.GetLeavesInBounds(pageScan.Min, pageScan.Max, excludeRedundantLeaves: true);

            // Prune leaf items outside of the timestamp bounds to avoid issues with out-of-bound leaves being processed.
            var leafItemToRank = page.GetLeafItemToRank();
            leafItemToRank = items.ToDictionary(x => (ICatalogLeafItem)x, x => leafItemToRank[x]);

            var storage = await _storageFactory.CreateAsync(pageScan, leafItemToRank);

            await AddAsync(items, storage);

            return CatalogPageScanResult.Processed;
        }

        private async Task AddAsync(List<CatalogLeafItem> items, ILatestPackageLeafStorage<T> storage)
        {
            await _storageService.AddAsync(items, storage, allowRetries: true);
            _logger.LogInformation("Updated latest leaf entities for {Count} items.", items.Count);
        }

        public Task<DriverResult> ProcessLeafAsync(CatalogLeafScan leafScan)
        {
            throw new NotImplementedException();
        }

        public Task StartAggregateAsync(CatalogIndexScan indexScan)
        {
            return Task.CompletedTask;
        }

        public Task<bool> IsAggregateCompleteAsync(CatalogIndexScan indexScan)
        {
            return Task.FromResult(true);
        }

        public Task FinalizeAsync(CatalogIndexScan indexScan)
        {
            return Task.CompletedTask;
        }

        public async Task DestroyOutputAsync()
        {
            await _storageFactory.DestroyAsync();
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.LoadPackageVersion
{
    public class LoadPackageVersionDriver : ICatalogLeafScanBatchDriver
    {
        private readonly PackageVersionStorageService _storageService;
        private readonly LatestLeafStorageService<PackageVersionEntity> _latestLeafStorageService;
        private readonly ILogger<LoadPackageVersionDriver> _logger;

        public LoadPackageVersionDriver(
            PackageVersionStorageService storageService,
            LatestLeafStorageService<PackageVersionEntity> latestLeafStorageService,
            ILogger<LoadPackageVersionDriver> logger)
        {
            _storageService = storageService;
            _latestLeafStorageService = latestLeafStorageService;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await _storageService.InitializeAsync();
        }

        public Task InitializeAsync(CatalogIndexScan indexScan)
        {
            return Task.CompletedTask;
        }

        public Task<CatalogIndexScanResult> ProcessIndexAsync(CatalogIndexScan indexScan)
        {
            return Task.FromResult(indexScan.OnlyLatestLeaves ? CatalogIndexScanResult.ExpandLatestLeaves : CatalogIndexScanResult.ExpandAllLeaves);
        }

        public Task<CatalogPageScanResult> ProcessPageAsync(CatalogPageScan pageScan)
        {
            return Task.FromResult(CatalogPageScanResult.ExpandRemoveDuplicates);
        }

        public async Task<BatchMessageProcessorResult<CatalogLeafScan>> ProcessLeavesAsync(IReadOnlyList<CatalogLeafScan> leafScans)
        {
            var storage = await _storageService.GetLatestPackageLeafStorageAsync();
            await _latestLeafStorageService.AddAsync(leafScans, storage);
            return BatchMessageProcessorResult<CatalogLeafScan>.Empty;
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
            await _storageService.DestroyAsync();
        }
    }
}

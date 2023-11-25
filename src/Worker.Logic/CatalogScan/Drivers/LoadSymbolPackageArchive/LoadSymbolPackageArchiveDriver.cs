// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.LoadSymbolPackageArchive
{
    public class LoadSymbolPackageArchiveDriver : ICatalogLeafScanBatchDriver
    {
        private readonly SymbolPackageFileService _symbolPackageFileService;
        private readonly ILogger<LoadSymbolPackageArchiveDriver> _logger;

        public LoadSymbolPackageArchiveDriver(SymbolPackageFileService symbolPackageFileService, ILogger<LoadSymbolPackageArchiveDriver> logger)
        {
            _symbolPackageFileService = symbolPackageFileService;
            _logger = logger;
        }

        public async Task InitializeAsync(CatalogIndexScan indexScan)
        {
            await _symbolPackageFileService.InitializeAsync();
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
            var failed = new List<CatalogLeafScan>();

            foreach (var group in leafScans.GroupBy(x => x.PackageId, StringComparer.OrdinalIgnoreCase))
            {
                var leafItems = group.Select(x => x.ToPackageIdentityCommit()).ToList();
                try
                {
                    await _symbolPackageFileService.UpdateBatchFromLeafItemsAsync(group.Key, leafItems);
                }
                catch (Exception ex) when (leafScans.Count != 1)
                {
                    _logger.LogError(ex, "Updating symbol package file info failed for {Id} with {Count} versions.", group.Key, leafItems.Count);
                    failed.AddRange(group);
                }
            }

            return new BatchMessageProcessorResult<CatalogLeafScan>(failed);
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
            await _symbolPackageFileService.DestroyAsync();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Worker.LoadPackageArchive
{
    public class LoadPackageArchiveDriver : ICatalogLeafScanBatchDriver
    {
        private readonly PackageFileService _packageFileService;
        private readonly ILogger<LoadPackageArchiveDriver> _logger;

        public LoadPackageArchiveDriver(PackageFileService packageFileService, ILogger<LoadPackageArchiveDriver> logger)
        {
            _packageFileService = packageFileService;
            _logger = logger;
        }

        public async Task InitializeAsync(CatalogIndexScan indexScan)
        {
            await _packageFileService.InitializeAsync();
        }

        public Task<CatalogIndexScanResult> ProcessIndexAsync(CatalogIndexScan indexScan)
        {

            return Task.FromResult(CatalogIndexScanResult.ExpandLatestLeaves);
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
                var leafItems = group.Select(x => x.GetLeafItem()).ToList();
                try
                {
                    await _packageFileService.UpdateBatchAsync(group.Key, leafItems);
                }
                catch (Exception ex) when (leafScans.Count != 1)
                {
                    _logger.LogError(ex, "Updating package file info failed for {Id} with {Count} versions.", group.Key, leafItems.Count);
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
    }
}

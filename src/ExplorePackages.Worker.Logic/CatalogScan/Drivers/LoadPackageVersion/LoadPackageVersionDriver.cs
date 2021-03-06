using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Worker.LoadPackageVersion
{
    public class LoadPackageVersionDriver : ICatalogLeafScanBatchDriver
    {
        private readonly PackageVersionStorageService _storage;
        private readonly LatestLeafStorageService<PackageVersionEntity> _storageService;
        private readonly ILogger<LoadPackageVersionDriver> _logger;

        public LoadPackageVersionDriver(
            PackageVersionStorageService storage,
            LatestLeafStorageService<PackageVersionEntity> storageService,
            ILogger<LoadPackageVersionDriver> logger)
        {
            _storage = storage;
            _storageService = storageService;
            _logger = logger;
        }

        public async Task InitializeAsync(CatalogIndexScan indexScan)
        {
            await _storage.InitializeAsync();
        }

        public Task<CatalogIndexScanResult> ProcessIndexAsync(CatalogIndexScan indexScan)
        {
            return Task.FromResult(CatalogIndexScanResult.ExpandLatestLeaves);
        }

        public Task<CatalogPageScanResult> ProcessPageAsync(CatalogPageScan pageScan)
        {
            throw new NotImplementedException();
        }

        public async Task<BatchMessageProcessorResult<CatalogLeafScan>> ProcessLeavesAsync(IReadOnlyList<CatalogLeafScan> leafScans)
        {
            var failed = new List<CatalogLeafScan>();

            foreach (var group in leafScans.GroupBy(x => x.PackageId, StringComparer.OrdinalIgnoreCase))
            {
                var packageId = group.Key;
                var leafItems = group.Select(x => x.GetLeafItem()).ToList();

                try
                {
                    await _storageService.AddAsync(packageId, leafItems, _storage, allowRetries: true);
                }
                catch (Exception ex) when (leafScans.Count != 1)
                {
                    _logger.LogError(ex, "Updating package package version info failed for {Id} with {Count} versions.", packageId, leafItems.Count);
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

        public Task StartCustomExpandAsync(CatalogIndexScan indexScan)
        {
            throw new NotImplementedException();
        }

        public Task<bool> IsCustomExpandCompleteAsync(CatalogIndexScan indexScan)
        {
            throw new NotImplementedException();
        }
    }
}

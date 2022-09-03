// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

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

        public async Task InitializeAsync(CatalogIndexScan indexScan)
        {
            await _storageService.InitializeAsync();
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
            var storage = await _storageService.GetLatestPackageLeafStorageAsync();
            foreach (var group in leafScans.GroupBy(x => x.PackageId, StringComparer.OrdinalIgnoreCase))
            {
                var leafItems = group.ToList();

                try
                {
                    await _latestLeafStorageService.AddAsync(leafItems, storage, allowRetries: true);
                }
                catch (Exception ex) when (leafScans.Count != 1)
                {
                    _logger.LogError(ex, "Updating package package version info failed for {Id} with {Count} versions.", group.Key, leafItems.Count);
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

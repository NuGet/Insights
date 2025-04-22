// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.BuildVersionSet
{
    public class BuildVersionSetDriver : ICatalogLeafScanBatchDriver
    {
        private readonly CatalogClient _catalogClient;
        private readonly VersionSetAggregateStorageService _aggregateStorageService;
        private readonly VersionSetService _versionSetService;

        public BuildVersionSetDriver(
            CatalogClient catalogClient,
            VersionSetAggregateStorageService aggregateStorageService,
            VersionSetService versionSetService)
        {
            _catalogClient = catalogClient;
            _aggregateStorageService = aggregateStorageService;
            _versionSetService = versionSetService;
        }

        public async Task InitializeAsync()
        {
            await _versionSetService.InitializeAsync();
        }

        public async Task InitializeAsync(CatalogIndexScan indexScan)
        {
            await _aggregateStorageService.InitializeAsync(indexScan.StorageSuffix);
        }

        public Task<CatalogIndexScanResult> ProcessIndexAsync(CatalogIndexScan indexScan)
        {
            return Task.FromResult(CatalogIndexScanResult.ExpandAllLeaves);
        }

        public async Task<CatalogPageScanResult> ProcessPageAsync(CatalogPageScan pageScan)
        {
            var page = await _catalogClient.GetCatalogPageAsync(pageScan.Url);

            var leafItems = page.GetLeavesInBounds(pageScan.Min, pageScan.Max, excludeRedundantLeaves: true);

            if (leafItems.Any())
            {
                var batch = new CatalogLeafBatchData(
                    leafItems.Max(x => x.CommitTimestamp),
                    leafItems
                        .Select(x => new CatalogLeafItemData(
                            x.PackageId,
                            x.ParsePackageVersion().ToNormalizedString(),
                            x.IsPackageDelete()))
                        .ToList());

                await _aggregateStorageService.AddBatchAsync(pageScan.StorageSuffix, batch);
            }

            return CatalogPageScanResult.Processed;
        }

        public Task<BatchMessageProcessorResult<CatalogLeafScan>> ProcessLeavesAsync(IReadOnlyList<CatalogLeafScan> leafScans)
        {
            throw new NotSupportedException();
        }

        public async Task StartAggregateAsync(CatalogIndexScan indexScan)
        {
            var descendingBatches = await _aggregateStorageService.GetDescendingBatchesAsync(indexScan.StorageSuffix);

            var idToVersionToDeleted = new CaseInsensitiveSortedDictionary<CaseInsensitiveSortedDictionary<bool>>();
            foreach (var batch in descendingBatches)
            {
                foreach (var leaf in batch.Leaves)
                {
                    if (!idToVersionToDeleted.TryGetValue(leaf.Id, out var versions))
                    {
                        versions = new CaseInsensitiveSortedDictionary<bool>();
                        idToVersionToDeleted.Add(leaf.Id, versions);
                    }

                    if (!versions.ContainsKey(leaf.Version))
                    {
                        versions.Add(leaf.Version, leaf.IsDeleted);
                    }
                }
            }

            await _versionSetService.UpdateAsync(indexScan.Max, idToVersionToDeleted);
        }

        public Task<bool> IsAggregateCompleteAsync(CatalogIndexScan indexScan)
        {
            return Task.FromResult(true);
        }

        public async Task FinalizeAsync(CatalogIndexScan indexScan)
        {
            await _aggregateStorageService.DeleteTableAsync(indexScan.StorageSuffix);
        }

        public async Task DestroyOutputAsync()
        {
            await _versionSetService.DestroyAsync();
        }
    }
}

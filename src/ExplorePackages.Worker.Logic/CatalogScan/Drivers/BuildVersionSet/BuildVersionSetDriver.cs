using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker.BuildVersionSet
{
    public class BuildVersionSetDriver : ICatalogLeafScanNonBatchDriver
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

        public async Task InitializeAsync(CatalogIndexScan indexScan)
        {
            await _aggregateStorageService.InitializeAsync(indexScan.StorageSuffix);
            await _versionSetService.InitializeAsync();
        }

        public Task<CatalogIndexScanResult> ProcessIndexAsync(CatalogIndexScan indexScan)
        {
            return Task.FromResult(CatalogIndexScanResult.ExpandAllLeaves);
        }

        public async Task<CatalogPageScanResult> ProcessPageAsync(CatalogPageScan pageScan)
        {
            var page = await _catalogClient.GetCatalogPageAsync(pageScan.Url);

            var leaves = page
                .GetLeavesInBounds(pageScan.Min, pageScan.Max, excludeRedundantLeaves: true)
                .Select(x => new CatalogLeafItemData(
                    x.PackageId.ToLowerInvariant(),
                    x.ParsePackageVersion().ToNormalizedString().ToLowerInvariant(),
                    x.IsPackageDelete()))
                .ToList();

            var pageData = new CatalogPageData(page.CommitTimestamp, leaves);

            await _aggregateStorageService.AddPageAsync(pageScan.StorageSuffix, pageData);

            return CatalogPageScanResult.Processed;
        }

        public Task<DriverResult> ProcessLeafAsync(CatalogLeafScan leafScan)
        {
            throw new NotImplementedException();
        }

        public async Task StartAggregateAsync(CatalogIndexScan indexScan)
        {
            var descendingPages = await _aggregateStorageService.GetDescendingPagesAsync(indexScan.StorageSuffix);

            var idToVersionToDeleted = new SortedDictionary<string, SortedDictionary<string, bool>>();
            
            foreach (var page in descendingPages)
            {
                foreach (var leaf in page.Leaves)
                {
                    if (!idToVersionToDeleted.TryGetValue(leaf.LowerId, out var versions))
                    {
                        versions = new SortedDictionary<string, bool>();
                        idToVersionToDeleted.Add(leaf.LowerId, versions);
                    }

                    if (!versions.ContainsKey(leaf.LowerVersion))
                    {
                        versions.Add(leaf.LowerVersion, leaf.IsDeleted);
                    }
                }
            }

            await _versionSetService.UpdateAsync(indexScan.Max.Value, idToVersionToDeleted);
        }

        public Task<bool> IsAggregateCompleteAsync(CatalogIndexScan indexScan)
        {
            return Task.FromResult(true);
        }

        public async Task FinalizeAsync(CatalogIndexScan indexScan)
        {
            await _aggregateStorageService.DeleteTableAsync(indexScan.StorageSuffix);
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

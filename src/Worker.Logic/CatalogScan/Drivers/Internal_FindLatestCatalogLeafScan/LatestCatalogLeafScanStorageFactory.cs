// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.FindLatestCatalogLeafScan
{
    public class LatestCatalogLeafScanStorageFactory : ILatestPackageLeafStorageFactory<CatalogLeafScan>
    {
        private readonly CatalogScanStorageService _storageService;

        public LatestCatalogLeafScanStorageFactory(
            CatalogScanStorageService storageService)
        {
            _storageService = storageService;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public Task DestroyAsync()
        {
            return Task.CompletedTask;
        }

        public async Task<ILatestPackageLeafStorage<CatalogLeafScan>> CreateAsync(CatalogPageScan pageScan, IReadOnlyDictionary<ICatalogLeafItem, int> leafItemToRank)
        {
            var indexScan = await _storageService.GetIndexScanAsync(pageScan.ParentDriverType.Value, pageScan.ParentScanId);
            var table = await _storageService.GetLeafScanTableAsync(indexScan.StorageSuffix);
            return new LatestCatalogLeafScanStorage(table, indexScan, pageScan.Url);
        }
    }
}

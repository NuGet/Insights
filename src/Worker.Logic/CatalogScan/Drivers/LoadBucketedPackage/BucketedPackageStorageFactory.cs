// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Insights.Worker.LoadBucketedPackage
{
    public class BucketedPackageStorageFactory : ILatestPackageLeafStorageFactory<BucketedPackage>
    {
        private readonly BucketedPackageService _service;

        public BucketedPackageStorageFactory(BucketedPackageService service)
        {
            _service = service;
        }

        public async Task InitializeAsync()
        {
            await _service.InitializeAsync();
        }

        public async Task<ILatestPackageLeafStorage<BucketedPackage>> CreateAsync(CatalogPageScan pageScan, IReadOnlyDictionary<ICatalogLeafItem, int> leafItemToRank)
        {
            return new BucketedPackageStorage(await _service.GetTableAsync(), pageScan.Url);
        }

        public async Task DestroyAsync()
        {
            await _service.DestroyAsync();
        }
    }
}

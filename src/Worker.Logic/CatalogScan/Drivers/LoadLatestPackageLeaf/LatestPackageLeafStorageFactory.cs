// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Insights.Worker.LoadLatestPackageLeaf
{
    public class LatestPackageLeafStorageFactory : ILatestPackageLeafStorageFactory<LatestPackageLeaf>
    {
        private readonly LatestPackageLeafService _service;

        public LatestPackageLeafStorageFactory(LatestPackageLeafService service)
        {
            _service = service;
        }

        public async Task InitializeAsync()
        {
            await _service.InitializeAsync();
        }

        public async Task<ILatestPackageLeafStorage<LatestPackageLeaf>> CreateAsync(CatalogPageScan pageScan, IReadOnlyDictionary<CatalogLeafItem, int> leafItemToRank)
        {
            return new LatestPackageLeafStorage(
                await _service.GetTableAsync(),
                leafItemToRank,
                pageScan.Rank,
                pageScan.Url);
        }
    }
}

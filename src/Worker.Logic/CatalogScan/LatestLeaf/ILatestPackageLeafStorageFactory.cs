// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public interface ILatestPackageLeafStorageFactory<T> where T : ILatestPackageLeaf
    {
        Task InitializeAsync();
        Task DestroyAsync();

        Task<ILatestPackageLeafStorage<T>> CreateAsync(
            CatalogPageScan pageScan,
            IReadOnlyDictionary<ICatalogLeafItem, int> leafItemToRank);
    }
}

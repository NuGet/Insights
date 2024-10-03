// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.StorageNoOpRetry;

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

        public async Task DestroyAsync()
        {
            await _service.DestroyAsync();
        }

        public async Task<ILatestPackageLeafStorage<LatestPackageLeaf>> CreateAsync(CatalogPageScan pageScan, IReadOnlyDictionary<ICatalogLeafItem, int> leafItemToRank)
        {
            return new LatestPackageLeafStorage(
                await _service.GetTableAsync(),
                leafItemToRank,
                pageScan.Rank,
                pageScan.Url);
        }

        private class LatestPackageLeafStorage : ILatestPackageLeafStorage<LatestPackageLeaf>
        {
            private readonly IReadOnlyDictionary<ICatalogLeafItem, int> _leafItemToRank;
            private readonly int _pageRank;
            private readonly string _pageUrl;

            public LatestPackageLeafStorage(
                TableClientWithRetryContext table,
                IReadOnlyDictionary<ICatalogLeafItem, int> leafItemToRank,
                int pageRank,
                string pageUrl)
            {
                Table = table;
                _leafItemToRank = leafItemToRank;
                _pageRank = pageRank;
                _pageUrl = pageUrl;
            }

            public TableClientWithRetryContext Table { get; }
            public string CommitTimestampColumnName => nameof(LatestPackageLeaf.CommitTimestamp);
            public EntityUpsertStrategy Strategy => EntityUpsertStrategy.ReadThenAdd;

            public (string PartitionKey, string RowKey) GetKey(ICatalogLeafItem item)
            {
                return (LatestPackageLeaf.GetPartitionKey(item.PackageId), LatestPackageLeaf.GetRowKey(item.PackageVersion));
            }

            public Task<LatestPackageLeaf> MapAsync(string partitionKey, string rowKey, ICatalogLeafItem item)
            {
                return Task.FromResult(new LatestPackageLeaf(
                    item,
                    partitionKey,
                    rowKey,
                    _leafItemToRank[item],
                    _pageRank,
                    _pageUrl));
            }
        }
    }
}

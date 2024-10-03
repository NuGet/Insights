// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.StorageNoOpRetry;

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

        private class BucketedPackageStorage : ILatestPackageLeafStorage<BucketedPackage>
        {
            private readonly string _pageUrl;

            public BucketedPackageStorage(TableClientWithRetryContext table, string pageUrl)
            {
                Table = table;
                _pageUrl = pageUrl;
            }

            public TableClientWithRetryContext Table { get; }
            public string CommitTimestampColumnName => nameof(BucketedPackage.CommitTimestamp);
            public EntityUpsertStrategy Strategy => EntityUpsertStrategy.AddOptimistically;

            public (string PartitionKey, string RowKey) GetKey(ICatalogLeafItem item)
            {
                var rowKey = BucketedPackage.GetRowKey(item);
                return (BucketedPackage.GetPartitionKey(rowKey), rowKey);
            }

            public Task<BucketedPackage> MapAsync(string partitionKey, string rowKey, ICatalogLeafItem item)
            {
                return Task.FromResult(new BucketedPackage(partitionKey, rowKey, item, _pageUrl));
            }
        }
    }
}

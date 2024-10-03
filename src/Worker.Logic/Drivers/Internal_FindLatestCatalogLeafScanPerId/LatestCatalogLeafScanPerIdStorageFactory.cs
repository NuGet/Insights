// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.StorageNoOpRetry;

namespace NuGet.Insights.Worker.FindLatestCatalogLeafScanPerId
{
    public class LatestCatalogLeafScanPerIdStorageFactory : ILatestPackageLeafStorageFactory<CatalogLeafScanPerId>
    {
        private readonly CatalogScanStorageService _storageService;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public LatestCatalogLeafScanPerIdStorageFactory(
            CatalogScanStorageService storageService,
            IOptions<NuGetInsightsWorkerSettings> options)
        {
            _storageService = storageService;
            _options = options;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public Task DestroyAsync()
        {
            return Task.CompletedTask;
        }

        public async Task<ILatestPackageLeafStorage<CatalogLeafScanPerId>> CreateAsync(CatalogPageScan pageScan, IReadOnlyDictionary<ICatalogLeafItem, int> leafItemToRank)
        {
            var indexScan = await _storageService.GetIndexScanAsync(pageScan.ParentDriverType.Value, pageScan.ParentScanId);
            var table = await _storageService.GetLeafScanTableAsync(indexScan.StorageSuffix);
            var bucketKeyFactory = CatalogScanDriverMetadata.GetBucketKeyFactory(indexScan.DriverType);
            return new LatestCatalogLeafScanPerIdStorage(table, indexScan, pageScan.Url, _options.Value.AppendResultStorageBucketCount, bucketKeyFactory);
        }

        private class LatestCatalogLeafScanPerIdStorage : ILatestPackageLeafStorage<CatalogLeafScanPerId>
        {
            private readonly CatalogIndexScan _indexScan;
            private readonly string _pageUrl;
            private readonly int _bucketCount;
            private readonly GetBucketKey _bucketKeyFactory;

            public LatestCatalogLeafScanPerIdStorage(TableClientWithRetryContext table, CatalogIndexScan indexScan, string pageUrl, int bucketCount, GetBucketKey bucketKeyFactory)
            {
                Table = table;
                _indexScan = indexScan;
                _pageUrl = pageUrl;
                _bucketCount = bucketCount;
                _bucketKeyFactory = bucketKeyFactory;
            }

            public TableClientWithRetryContext Table { get; }
            public string CommitTimestampColumnName => nameof(CatalogLeafScan.CommitTimestamp);
            public EntityUpsertStrategy Strategy => EntityUpsertStrategy.AddOptimistically;

            public (string PartitionKey, string RowKey) GetKey(ICatalogLeafItem item)
            {
                var lowerId = item.PackageId.ToLowerInvariant();

                // Use the bucket produced from the bucket key for the page ID so leaf scans of the same bucket are adjacent, allowing more efficient batching
                var bucketKey = _bucketKeyFactory(lowerId, string.Empty); // no version context allowed
                var bucket = StorageUtility.GetBucket(_bucketCount, bucketKey);
                var pageId = $"B{bucket:D3}";

                var partitionKey = CatalogLeafScan.GetPartitionKey(_indexScan.ScanId, pageId);
                var rowKey = lowerId;

                return (partitionKey, rowKey);
            }

            public Task<CatalogLeafScanPerId> MapAsync(string partitionKey, string rowKey, ICatalogLeafItem item)
            {
                var pageId = partitionKey.Substring(_indexScan.ScanId.Length + 1);

                return Task.FromResult(new CatalogLeafScanPerId(partitionKey, rowKey, _indexScan.StorageSuffix, _indexScan.ScanId, pageId)
                {
                    DriverType = _indexScan.DriverType,
                    Min = _indexScan.Min,
                    Max = _indexScan.Max,
                    BucketRanges = _indexScan.BucketRanges,
                    Url = item.Url,
                    PageUrl = _pageUrl,
                    LeafType = item.LeafType,
                    CommitId = item.CommitId,
                    CommitTimestamp = item.CommitTimestamp,
                    PackageId = item.PackageId,
                    PackageVersion = item.PackageVersion,
                });
            }
        }
    }
}

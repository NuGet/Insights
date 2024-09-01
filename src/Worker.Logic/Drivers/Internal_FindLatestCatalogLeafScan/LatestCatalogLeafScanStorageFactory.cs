// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.StorageNoOpRetry;

namespace NuGet.Insights.Worker.FindLatestCatalogLeafScan
{
    public class LatestCatalogLeafScanStorageFactory : ILatestPackageLeafStorageFactory<CatalogLeafScan>
    {
        private readonly CatalogScanStorageService _storageService;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public LatestCatalogLeafScanStorageFactory(
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

        public async Task<ILatestPackageLeafStorage<CatalogLeafScan>> CreateAsync(CatalogPageScan pageScan, IReadOnlyDictionary<ICatalogLeafItem, int> leafItemToRank)
        {
            var indexScan = await _storageService.GetIndexScanAsync(pageScan.ParentDriverType.Value, pageScan.ParentScanId);
            var table = await _storageService.GetLeafScanTableAsync(indexScan.StorageSuffix);
            var bucketKeyFactory = CatalogScanDriverMetadata.GetBucketKeyFactory(indexScan.DriverType);
            return new LatestCatalogLeafScanStorage(table, indexScan, pageScan.Url, _options.Value.AppendResultStorageBucketCount, bucketKeyFactory);
        }

        private class LatestCatalogLeafScanStorage : ILatestPackageLeafStorage<CatalogLeafScan>
        {
            private readonly CatalogIndexScan _indexScan;
            private readonly string _pageUrl;
            private readonly int _bucketCount;
            private readonly GetBucketKey _bucketKeyFactory;

            public LatestCatalogLeafScanStorage(TableClientWithRetryContext table, CatalogIndexScan indexScan, string pageUrl, int bucketCount, GetBucketKey bucketKeyFactory)
            {
                Table = table;
                _indexScan = indexScan;
                _pageUrl = pageUrl;
                _bucketCount = bucketCount;
                _bucketKeyFactory = bucketKeyFactory;
            }

            public TableClientWithRetryContext Table { get; }
            public string CommitTimestampColumnName => nameof(CatalogLeafScan.CommitTimestamp);

            public (string PartitionKey, string RowKey) GetKey(ICatalogLeafItem item)
            {
                var lowerId = item.PackageId.ToLowerInvariant();
                var normalizedVersion = NuGetVersion.Parse(item.PackageVersion).ToNormalizedString().ToLowerInvariant();

                // Use the bucket produced from the bucket key for the page ID so leaf scans of the same bucket are adjacent, allowing more efficient batching
                var bucketKey = _bucketKeyFactory(lowerId, normalizedVersion);
                var bucket = StorageUtility.GetBucket(_bucketCount, bucketKey);
                var pageId = $"B{bucket:D3}";

                var partitionKey = CatalogLeafScan.GetPartitionKey(_indexScan.ScanId, pageId);
                var rowKey = $"{lowerId}${normalizedVersion}"; // can't use identity because forward slashes are not allowed in row keys

                return (partitionKey, rowKey);
            }

            public Task<CatalogLeafScan> MapAsync(string partitionKey, string rowKey, ICatalogLeafItem item)
            {
                var pageId = partitionKey.Substring(_indexScan.ScanId.Length + 1);

                return Task.FromResult(new CatalogLeafScan(partitionKey, rowKey, _indexScan.StorageSuffix, _indexScan.ScanId, pageId)
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

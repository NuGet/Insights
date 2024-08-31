// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.StorageNoOpRetry;

namespace NuGet.Insights.Worker.LoadBucketedPackage
{
    public class BucketedPackageStorage : ILatestPackageLeafStorage<BucketedPackage>
    {
        private readonly string _pageUrl;

        public BucketedPackageStorage(TableClientWithRetryContext table, string pageUrl)
        {
            Table = table;
            _pageUrl = pageUrl;
        }

        public TableClientWithRetryContext Table { get; }

        public (string PartitionKey, string RowKey) GetKey(ICatalogLeafItem item)
        {
            var rowKey = BucketedPackage.GetRowKey(item);
            return (BucketedPackage.GetPartitionKey(rowKey), rowKey);
        }

        public string CommitTimestampColumnName => nameof(BucketedPackage.CommitTimestamp);

        public Task<BucketedPackage> MapAsync(string partitionKey, string rowKey, ICatalogLeafItem item)
        {
            return Task.FromResult(new BucketedPackage(partitionKey, rowKey, item, _pageUrl));
        }
    }
}

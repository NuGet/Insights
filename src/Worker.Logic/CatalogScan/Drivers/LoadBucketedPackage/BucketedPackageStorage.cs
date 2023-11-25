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

        public string GetPartitionKey(ICatalogLeafItem item)
        {
            return BucketedPackage.GetPartitionKey(item);
        }

        public string GetRowKey(ICatalogLeafItem item)
        {
            return BucketedPackage.GetRowKey(item);
        }

        public string CommitTimestampColumnName => nameof(BucketedPackage.CommitTimestamp);

        public Task<BucketedPackage> MapAsync(ICatalogLeafItem item)
        {
            return Task.FromResult(new BucketedPackage(item, _pageUrl));
        }
    }
}

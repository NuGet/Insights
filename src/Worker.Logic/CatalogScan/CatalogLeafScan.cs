// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.Serialization;
using Azure;

namespace NuGet.Insights.Worker
{
    public class CatalogLeafScan : ILatestPackageLeaf, ICatalogLeafItem
    {
        public CatalogLeafScan()
        {
        }

        public CatalogLeafScan(string storageSuffix, string scanId, string pageId, string leafId)
        {
            StorageSuffix = storageSuffix;
            PartitionKey = GetPartitionKey(scanId, pageId);
            RowKey = leafId;
            ScanId = scanId;
            PageId = pageId;
            Created = DateTimeOffset.UtcNow;
        }

        [IgnoreDataMember]
        public string LeafId => RowKey;

        public string StorageSuffix { get; set; }
        public DateTimeOffset Created { get; set; }
        public CatalogScanDriverType DriverType { get; set; }
        public DateTimeOffset Min { get; set; }
        public DateTimeOffset Max { get; set; }
        public string BucketRanges { get; set; }
        public string ScanId { get; set; }
        public string PageId { get; set; }
        public string Url { get; set; }
        public string PageUrl { get; set; }
        public CatalogLeafType LeafType { get; set; }
        public string CommitId { get; set; }
        public DateTimeOffset CommitTimestamp { get; set; }
        public string PackageId { get; set; }
        public string PackageVersion { get; set; }
        public DateTimeOffset? NextAttempt { get; set; }
        public int AttemptCount { get; set; }

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
        public Guid? ClientRequestId { get; set; }

        public IPackageIdentityCommit ToPackageIdentityCommit()
        {
            return new PackageIdentityCommit
            {
                PackageId = PackageId,
                PackageVersion = PackageVersion,
                LeafType = LeafType,
                CommitTimestamp = BucketRanges is null ? CommitTimestamp : null,
            };
        }

        public static string GetPartitionKey(string scanId, string pageId)
        {
            return $"{scanId}-{pageId}";
        }
    }
}

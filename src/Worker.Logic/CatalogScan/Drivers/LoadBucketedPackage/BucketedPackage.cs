// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using Azure;

namespace NuGet.Insights.Worker.LoadBucketedPackage
{
    public class BucketedPackage : ILatestPackageLeaf, ICatalogLeafItem
    {
        public const int BucketCount = 1000;

        public BucketedPackage()
        {
        }

        public BucketedPackage(ICatalogLeafItem item, string pageUrl)
        {
            PartitionKey = GetPartitionKey(item);
            RowKey = GetRowKey(item);
            Url = item.Url;
            LeafType = item.LeafType;
            CommitId = item.CommitId;
            CommitTimestamp = item.CommitTimestamp;
            PackageId = item.PackageId;
            PackageVersion = item.PackageVersion;
            PageUrl = pageUrl;
        }

        public string Url { get; set; }
        public CatalogLeafType LeafType { get; set; }
        public string CommitId { get; set; }
        public DateTimeOffset CommitTimestamp { get; set; }
        public string PackageId { get; set; }
        public string PackageVersion { get; set; }
        public string PageUrl { get; set; }

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
        public Guid? ClientRequestId { get; set; }

        public int GetBucket()
        {
            return ParseBucketString(PartitionKey);
        }

        public static string GetBucketString(int bucket)
        {
            return $"B{bucket:D3}";
        }

        public static int ParseBucketString(string bucketString)
        {
            if (bucketString[0] != 'B')
            {
                throw new ArgumentException("The bucket string must start with a 'B'.", nameof(bucketString));
            }

            return int.Parse(bucketString.Substring(1), CultureInfo.InvariantCulture);
        }

        public static string GetPartitionKey(ICatalogLeafItem item)
        {
            var rowKey = GetRowKey(item);
            var bucket = StorageUtility.GetBucket(BucketCount, rowKey);
            return GetBucketString(bucket);
        }

        public static string GetRowKey(ICatalogLeafItem item)
        {
            return item.PackageId.ToLowerInvariant() + "$" + item.ParsePackageVersion().ToNormalizedString().ToLowerInvariant();
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;

namespace NuGet.Insights.Worker.LoadBucketedPackage
{
    public class BucketedPackage : ILatestPackageLeaf, ICatalogLeafItem
    {
        public const int BucketCount = 1000;

        public BucketedPackage()
        {
        }

        public BucketedPackage(string partitionKey, string rowKey, ICatalogLeafItem item, string pageUrl)
        {
#if DEBUG
            if (GetPartitionKey(rowKey) != partitionKey)
            {
                throw new ArgumentException(nameof(partitionKey));
            }

            if (GetRowKey(item) != rowKey)
            {
                throw new ArgumentException(nameof(rowKey));
            }
#endif

            PartitionKey = partitionKey;
            RowKey = rowKey;
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

        public static string GetPartitionKey(string rowKey)
        {
            // bucketize by the identity string "{id}/{version}" instead of "{id}${version}", to match other buckets
            var dollarIndex = rowKey.IndexOf('$', StringComparison.Ordinal);
            var identity = PackageRecordExtensions.GetIdentity(rowKey.Substring(0, dollarIndex), rowKey.Substring(dollarIndex + 1));
            var bucket = StorageUtility.GetBucket(BucketCount, identity);
            return GetBucketString(bucket);
        }

        public static string GetRowKey(ICatalogLeafItem item)
        {
            return $"{item.PackageId.ToLowerInvariant()}${item.ParsePackageVersion().ToNormalizedString().ToLowerInvariant()}";
        }
    }
}

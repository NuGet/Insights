using System;
using Azure;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogLeafScan : ILatestPackageLeaf
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

        public string GetLeafId()
        {
            return RowKey;
        }

        public string StorageSuffix { get; set; }
        public DateTimeOffset Created { get; set; }
        public CatalogScanDriverType DriverType { get; set; }
        public string DriverParameters { get; set; }
        public string ScanId { get; set; }
        public string PageId { get; set; }
        public string Url { get; set; }
        public CatalogLeafType LeafType { get; set; }
        public string CommitId { get; set; }
        public DateTimeOffset CommitTimestamp { get; set; } = new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public string PackageId { get; set; }
        public string PackageVersion { get; set; }
        public DateTimeOffset? NextAttempt { get; set; }
        public int AttemptCount { get; set; }

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public static string GetPartitionKey(string scanId, string pageId)
        {
            return $"{scanId}-{pageId}";
        }

        public CatalogLeafItem ToLeafItem()
        {
            return new CatalogLeafItem
            {
                Url = Url,
                Type = LeafType,
                CommitId = CommitId,
                CommitTimestamp = CommitTimestamp,
                PackageId = PackageId,
                PackageVersion = PackageVersion,
            };
        }
    }
}

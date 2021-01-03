using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogLeafScan : TableEntity, ILatestPackageLeaf
    {
        public CatalogLeafScan(string storageSuffix, string scanId, string pageId, string leafId)
        {
            StorageSuffix = storageSuffix;
            PartitionKey = GetPartitionKey(scanId, pageId);
            RowKey = leafId;
            ScanId = scanId;
            PageId = pageId;
            Created = DateTimeOffset.UtcNow;
        }

        public CatalogLeafScan()
        {
        }

        [IgnoreProperty]
        public string LeafId => RowKey;

        [IgnoreProperty]
        public CatalogScanDriverType ParsedDriverType
        {
            get => Enum.Parse<CatalogScanDriverType>(DriverType);
            set => DriverType = value.ToString();
        }

        [IgnoreProperty]
        public CatalogLeafType ParsedLeafType
        {
            get => Enum.Parse<CatalogLeafType>(LeafType);
            set => LeafType = value.ToString();
        }

        public string StorageSuffix { get; set; }
        public DateTimeOffset Created { get; set; }
        public string DriverType { get; set; }
        public string DriverParameters { get; set; }
        public string ScanId { get; set; }
        public string PageId { get; set; }
        public string Url { get; set; }
        public string LeafType { get; set; }
        public string CommitId { get; set; }
        public DateTimeOffset CommitTimestamp { get; set; }
        public string PackageId { get; set; }
        public string PackageVersion { get; set; }
        public DateTimeOffset? NextAttempt { get; set; }
        public int AttemptCount { get; set; }

        public static string GetPartitionKey(string scanId, string pageId)
        {
            return $"{scanId}-{pageId}";
        }
    }
}

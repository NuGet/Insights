using System;
using Azure;
using Azure.Data.Tables;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogIndexScan : ITableEntity
    {
        public CatalogIndexScan()
        {
        }

        public CatalogIndexScan(string cursorName, string scanId, string storageSuffix)
        {
            PartitionKey = cursorName ?? throw new ArgumentNullException(nameof(cursorName)); // empty string is allowed
            RowKey = scanId;
            StorageSuffix = storageSuffix;
            Created = DateTimeOffset.UtcNow;
        }

        public string GetCursorName()
        {
            return PartitionKey;
        }

        public string GetScanId()
        {
            return RowKey;
        }

        public CatalogIndexScanResult? GetResult()
        {
            return Result != null ? Enum.Parse<CatalogIndexScanResult>(Result) : null;
        }

        public void SetResult(CatalogIndexScanResult result)
        {
            Result = result.ToString();
        }

        public string StorageSuffix { get; set; }
        public DateTimeOffset Created { get; set; }
        public CatalogIndexScanState State { get; set; }
        public CatalogScanDriverType DriverType { get; set; }
        public string DriverParameters { get; set; }
        public DateTimeOffset? Min { get; set; }
        public DateTimeOffset? Max { get; set; }
        public DateTimeOffset? Started { get; set; }
        public string Result { get; set; }
        public DateTimeOffset? Completed { get; set; }
        public bool ContinueUpdate { get; set; }

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}

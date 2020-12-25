using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogIndexScan : TableEntity
    {
        public CatalogIndexScan(string cursorName, string scanId, string storageSuffix) : this()
        {
            PartitionKey = cursorName;
            RowKey = scanId;
            StorageSuffix = storageSuffix;
            Created = DateTimeOffset.UtcNow;
        }

        public CatalogIndexScan()
        {
        }

        [IgnoreProperty]
        public string CursorName => PartitionKey;

        [IgnoreProperty]
        public string ScanId => RowKey;

        [IgnoreProperty]
        public CatalogScanState ParsedState
        {
            get => Enum.Parse<CatalogScanState>(State);
            set => State = value.ToString();
        }

        [IgnoreProperty]
        public CatalogScanType ParsedScanType
        {
            get => Enum.Parse<CatalogScanType>(ScanType);
            set => ScanType = value.ToString();
        }

        public string StorageSuffix { get; set; }
        public DateTimeOffset Created { get; set; }
        public string State { get; set; }
        public string ScanType { get; set; }
        public string ScanParameters { get; set; }
        public DateTimeOffset? Min { get; set; }
        public DateTimeOffset? Max { get; set; }
    }
}

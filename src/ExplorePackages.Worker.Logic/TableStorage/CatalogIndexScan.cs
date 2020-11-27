using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogIndexScan : TableEntity
    {
        public CatalogIndexScan(string scanId, string storageSuffix) : this()
        {
            PartitionKey = scanId;
            StorageSuffix = storageSuffix;
        }

        public CatalogIndexScan()
        {
            RowKey = string.Empty;
        }

        [IgnoreProperty]
        public string ScanId => PartitionKey;

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
        public string State { get; set; }
        public string ScanType { get; set; }
        public string ScanParameters { get; set; }
        public DateTimeOffset? Min { get; set; }
        public DateTimeOffset? Max { get; set; }
        public string CursorName { get; set; }
    }
}

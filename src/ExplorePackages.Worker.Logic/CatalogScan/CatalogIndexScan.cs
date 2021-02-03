using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogIndexScan : TableEntity
    {
        public CatalogIndexScan(string cursorName, string scanId, string storageSuffix) : this()
        {
            PartitionKey = cursorName ?? throw new ArgumentNullException(nameof(cursorName)); // empty string is allowed
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
        public CatalogIndexScanState ParsedState
        {
            get => Enum.Parse<CatalogIndexScanState>(State);
            set => State = value.ToString();
        }

        [IgnoreProperty]
        public CatalogScanDriverType ParsedDriverType
        {
            get => Enum.Parse<CatalogScanDriverType>(DriverType);
            set => DriverType = value.ToString();
        }

        [IgnoreProperty]
        public CatalogIndexScanResult? ParsedResult
        {
            get => Result != null ? Enum.Parse<CatalogIndexScanResult>(Result) : null;
            set => Result = value.ToString();
        }

        public string StorageSuffix { get; set; }
        public DateTimeOffset Created { get; set; }
        public string State { get; set; }
        public string DriverType { get; set; }
        public string DriverParameters { get; set; }
        public DateTimeOffset? Min { get; set; }
        public DateTimeOffset? Max { get; set; }
        public DateTimeOffset? Started { get; set; }
        public string Result { get; set; }
        public DateTimeOffset? Completed { get; set; }
    }
}

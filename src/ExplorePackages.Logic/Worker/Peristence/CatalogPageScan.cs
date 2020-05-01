using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class CatalogPageScan : TableEntity
    {
        public CatalogPageScan(string scanId, string pageId)
        {
            PartitionKey = scanId;
            RowKey = pageId;
        }

        public CatalogPageScan()
        {
        }

        [IgnoreProperty]
        public string ScanId => PartitionKey;

        [IgnoreProperty]
        public string PageId => RowKey;

        [IgnoreProperty]
        public CatalogScanType ParsedScanType
        {
            get => Enum.Parse<CatalogScanType>(ScanType);
            set => ScanType = value.ToString();
        }

        public string ScanType { get; set; }
        public DateTimeOffset Min { get; set; }
        public DateTimeOffset Max { get; set; }
        public string Url { get; set; }
    }
}

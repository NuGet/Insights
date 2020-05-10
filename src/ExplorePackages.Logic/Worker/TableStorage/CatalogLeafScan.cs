using System;
using Knapcode.ExplorePackages.Entities;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class CatalogLeafScan : TableEntity
    {
        public CatalogLeafScan(string scanId, string pageId, string leafId)
        {
            PartitionKey = GetPartitionKey(scanId, pageId);
            RowKey = leafId;
            ScanId = scanId;
            PageId = pageId;
        }

        public CatalogLeafScan()
        {
        }

        [IgnoreProperty]
        public string LeafId => RowKey;

        [IgnoreProperty]
        public CatalogScanType ParsedScanType
        {
            get => Enum.Parse<CatalogScanType>(ScanType);
            set => ScanType = value.ToString();
        }

        [IgnoreProperty]
        public CatalogLeafType ParsedLeafType
        {
            get => Enum.Parse<CatalogLeafType>(LeafType);
            set => LeafType = value.ToString();
        }

        public string ScanType { get; set; }
        public string ScanId { get; set; }
        public string PageId { get; set; }
        public string LeafType { get; set; }
        public string Url { get; set; }

        public static string GetPartitionKey(string scanId, string pageId)
        {
            return $"{scanId}-{pageId}";
        }
    }
}

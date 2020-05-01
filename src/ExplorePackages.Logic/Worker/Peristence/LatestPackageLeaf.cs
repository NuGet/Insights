using System;
using Knapcode.ExplorePackages.Entities;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class LatestPackageLeaf : TableEntity
    {
        public LatestPackageLeaf(string scanId, string lowerId, string lowerVersion)
        {
            PartitionKey = GetPartitionKey(scanId, lowerId);
            RowKey = lowerVersion;
            ScanId = scanId;
            LowerId = lowerId;
        }

        public LatestPackageLeaf()
        {
        }

        [IgnoreProperty]
        public string LowerVersion => RowKey;

        [IgnoreProperty]
        public CatalogLeafType ParsedType
        {
            get => Enum.Parse<CatalogLeafType>(Type);
            set => Type = value.ToString();
        }

        public string ScanId { get; set; }
        public string LowerId { get; set; }
        public DateTimeOffset CommitTimestamp { get; set; }
        public string Type { get; set; }
        public string Url { get; set; }

        public static string GetPartitionKey(string scanId, string lowerId)
        {
            return $"{scanId}-{lowerId}";
        }
    }
}

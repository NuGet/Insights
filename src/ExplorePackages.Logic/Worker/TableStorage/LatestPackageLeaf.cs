using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class LatestPackageLeaf : TableEntity
    {
        public LatestPackageLeaf(string prefix, string lowerId, string lowerVersion)
        {
            PartitionKey = GetPartitionKey(prefix, lowerId);
            RowKey = lowerVersion;
            Prefix = prefix;
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

        public string Prefix { get; set; }
        public string LowerId { get; set; }
        public DateTimeOffset CommitTimestamp { get; set; }
        public string Type { get; set; }
        public string Url { get; set; }

        public static string GetPartitionKey(string prefix, string lowerId)
        {
            return $"{prefix}${lowerId}";
        }
    }
}

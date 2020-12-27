using System;
using Microsoft.WindowsAzure.Storage.Table;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Worker.FindLatestLeaves
{
    public class LatestPackageLeaf : TableEntity
    {
        public LatestPackageLeaf(string prefix, CatalogLeafItem item)
        {
            PartitionKey = GetPartitionKey(prefix, item.PackageId);
            RowKey = GetRowKey(item.PackageVersion);
            Prefix = prefix;
            Url = item.Url;
            ParsedLeafType = item.Type;
            CommitId = item.CommitId;
            CommitTimestamp = item.CommitTimestamp;
            PackageId = item.PackageId;
            PackageVersion = item.PackageVersion;
        }

        public LatestPackageLeaf()
        {
        }

        [IgnoreProperty]
        public string LowerVersion => RowKey;

        [IgnoreProperty]
        public CatalogLeafType ParsedLeafType
        {
            get => Enum.Parse<CatalogLeafType>(LeafType);
            set => LeafType = value.ToString();
        }

        public string Prefix { get; set; }
        public string Url { get; set; }
        public string LeafType { get; set; }
        public string CommitId { get; set; }
        public DateTimeOffset CommitTimestamp { get; set; }
        public string PackageId { get; set; }
        public string PackageVersion { get; set; }

        public static string GetPartitionKey(string prefix, string id)
        {
            return $"{prefix}${id.ToLowerInvariant()}";
        }

        public static string GetRowKey(string version)
        {
            return NuGetVersion.Parse(version).ToNormalizedString().ToLowerInvariant();
        }
    }
}

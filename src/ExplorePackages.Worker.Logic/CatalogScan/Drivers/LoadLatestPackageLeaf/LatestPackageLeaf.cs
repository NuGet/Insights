using System;
using Azure;
using Microsoft.WindowsAzure.Storage.Table;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Worker.LoadLatestPackageLeaf
{
    public class LatestPackageLeaf : TableEntity, ILatestPackageLeaf, Azure.Data.Tables.ITableEntity
    {
        public LatestPackageLeaf(CatalogLeafItem item, int leafRank, int pageRank, string pageUrl)
        {
            PartitionKey = GetPartitionKey(item.PackageId);
            RowKey = GetRowKey(item.PackageVersion);
            Url = item.Url;
            ParsedLeafType = item.Type;
            CommitId = item.CommitId;
            CommitTimestamp = item.CommitTimestamp;
            PackageId = item.PackageId;
            PackageVersion = item.PackageVersion;
            LeafRank = leafRank;
            PageRank = pageRank;
            PageUrl = pageUrl;
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

        public string Url { get; set; }
        public string LeafType { get; set; }
        public string CommitId { get; set; }
        public DateTimeOffset CommitTimestamp { get; set; }
        public string PackageId { get; set; }
        public string PackageVersion { get; set; }
        public int LeafRank { get; set; }
        public int PageRank { get; set; }
        public string PageUrl { get; set; }

        DateTimeOffset? Azure.Data.Tables.ITableEntity.Timestamp
        {
            get => Timestamp;
            set => Timestamp = value.Value;
        }

        ETag Azure.Data.Tables.ITableEntity.ETag
        {
            get => new ETag(ETag);
            set => ETag = value.ToString();
        }

        public CatalogLeafItem ToLeafItem()
        {
            return new CatalogLeafItem
            {
                Url = Url,
                Type = ParsedLeafType,
                CommitId = CommitId,
                CommitTimestamp = CommitTimestamp,
                PackageId = PackageId,
                PackageVersion = PackageVersion,
            };
        }

        public static string GetPartitionKey(string id)
        {
            return id.ToLowerInvariant();
        }

        public static string GetRowKey(string version)
        {
            return NuGetVersion.Parse(version).ToNormalizedString().ToLowerInvariant();
        }
    }
}

using System;
using Microsoft.WindowsAzure.Storage.Table;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Worker.LoadPackageVersion
{
    public class PackageVersionEntity : TableEntity, ILatestPackageLeaf
    {
        public PackageVersionEntity(
            CatalogLeafItem item,
            DateTimeOffset? created,
            bool? listed,
            SemVerType? semVerType)
        {
            PartitionKey = GetPartitionKey(item.PackageId);
            RowKey = GetRowKey(item.PackageVersion);
            Url = item.Url;
            ParsedLeafType = item.Type;
            CommitId = item.CommitId;
            CommitTimestamp = item.CommitTimestamp;
            PackageId = item.PackageId;
            PackageVersion = item.PackageVersion;
            Created = created;
            IsListed = listed;
            ParsedSemVerType = semVerType;
        }

        public PackageVersionEntity()
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

        [IgnoreProperty]
        public SemVerType? ParsedSemVerType
        {
            get => SemVerType == null ? null : Enum.Parse<SemVerType>(SemVerType);
            set => SemVerType = value?.ToString();
        }

        public string Prefix { get; set; }
        public string Url { get; set; }
        public string LeafType { get; set; }
        public string CommitId { get; set; }
        public DateTimeOffset CommitTimestamp { get; set; }
        public string PackageId { get; set; }
        public string PackageVersion { get; set; }
        public DateTimeOffset? Created { get; set; }
        public bool? IsListed { get; set; }
        public string SemVerType { get; set; }

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

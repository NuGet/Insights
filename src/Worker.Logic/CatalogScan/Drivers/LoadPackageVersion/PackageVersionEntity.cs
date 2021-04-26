using System;
using Azure;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Worker.LoadPackageVersion
{
    public class PackageVersionEntity : ILatestPackageLeaf
    {
        public PackageVersionEntity()
        {
        }

        public PackageVersionEntity(
            CatalogLeafItem item,
            DateTimeOffset? created,
            bool? listed,
            SemVerType? semVerType)
        {
            PartitionKey = GetPartitionKey(item.PackageId);
            RowKey = GetRowKey(item.PackageVersion);
            Url = item.Url;
            LeafType = item.Type;
            CommitId = item.CommitId;
            CommitTimestamp = item.CommitTimestamp;
            PackageId = item.PackageId;
            PackageVersion = item.PackageVersion;
            Created = created;
            IsListed = listed;
            SemVerType = semVerType?.ToString();
        }

        public string Prefix { get; set; }
        public string Url { get; set; }
        public CatalogLeafType LeafType { get; set; }
        public string CommitId { get; set; }
        public DateTimeOffset CommitTimestamp { get; set; }
        public string PackageId { get; set; }
        public string PackageVersion { get; set; }
        public DateTimeOffset? Created { get; set; }
        public bool? IsListed { get; set; }
        public string SemVerType { get; set; }

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public string GetLowerId()
        {
            return PartitionKey;
        }

        public string GetLowerVersion()
        {
            return RowKey;
        }

        public SemVerType? GetSemVerType()
        {
            if (SemVerType == null)
            {
                return null;
            }

            return Enum.Parse<SemVerType>(SemVerType);
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

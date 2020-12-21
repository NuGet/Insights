using System;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Worker
{
    public class PackageRecord
    {
        public PackageRecord()
        {
        }

        public PackageRecord(Guid? scanId, DateTimeOffset? scanTimestamp, PackageDeleteCatalogLeaf leaf)
            : this(scanId, scanTimestamp, leaf.PackageId, leaf.PackageVersion, leaf.CommitTimestamp, created: null)
        {
        }

        public PackageRecord(Guid? scanId, DateTimeOffset? scanTimestamp, PackageDetailsCatalogLeaf leaf)
            : this(scanId, scanTimestamp, leaf.PackageId, leaf.PackageVersion, leaf.CommitTimestamp, leaf.Created)
        {
        }

        public PackageRecord(Guid? scanId, DateTimeOffset? scanTimestamp, string id, string version, DateTimeOffset catalogCommitTimestamp, DateTimeOffset? created)
        {
            ScanId = scanId;
            ScanTimestamp = scanTimestamp;
            Id = id;
            Version = NuGetVersion.Parse(version).ToNormalizedString();
            CatalogCommitTimestamp = catalogCommitTimestamp;
            Created = created;
        }

        public Guid? ScanId { get; set; }
        public DateTimeOffset? ScanTimestamp { get; set; }
        public string Id { get; set; }
        public string Version { get; set; }
        public DateTimeOffset CatalogCommitTimestamp { get; set; }
        public DateTimeOffset? Created { get; set; }
    }
}

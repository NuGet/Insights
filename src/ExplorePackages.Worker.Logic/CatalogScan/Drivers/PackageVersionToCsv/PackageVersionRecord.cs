using System;
using Knapcode.ExplorePackages.Worker.LoadPackageVersion;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Worker.PackageVersionToCsv
{
    public partial record PackageVersionRecord : PackageRecord, ICsvRecord<PackageVersionRecord>
    {
        public PackageVersionRecord()
        {
        }

        public PackageVersionRecord(Guid? scanId, DateTimeOffset? scanTimestamp, PackageVersionEntity entity) : base(
            scanId,
            scanTimestamp,
            entity.PackageId,
            entity.PackageVersion,
            entity.CommitTimestamp,
            entity.Created)
        {
            ResultType = entity.ParsedLeafType == CatalogLeafType.PackageDelete ? PackageVersionResultType.Deleted : PackageVersionResultType.Available;
            IsListed = entity.IsListed;
            SemVer2 = entity.ParsedSemVerType.HasValue ? entity.ParsedSemVerType.Value.IsSemVer2() : null;
        }

        public PackageVersionResultType ResultType { get; set; }

        public bool? IsListed { get; set; }
        public bool? SemVer2 { get; set; }
        public SemVerType? SemVerType { get; set; }

        public bool IsLatest { get; set; }
        public bool IsLatestStable { get; set; }
        public bool IsLatestSemVer2 { get; set; }
        public bool IsLatestStableSemVer2 { get; set; }
    }
}

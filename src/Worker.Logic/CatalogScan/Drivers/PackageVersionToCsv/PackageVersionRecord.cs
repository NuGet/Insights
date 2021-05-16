using System;
using NuGet.Insights.Worker.LoadPackageVersion;

namespace NuGet.Insights.Worker.PackageVersionToCsv
{
    public partial record PackageVersionRecord : PackageRecord, ICsvRecord
    {
        public PackageVersionRecord()
        {
        }

        public PackageVersionRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageVersionEntity entity) : base(
            scanId,
            scanTimestamp,
            entity.PackageId,
            entity.PackageVersion,
            entity.CommitTimestamp,
            entity.Created)
        {
            ResultType = entity.LeafType == CatalogLeafType.PackageDelete ? PackageVersionResultType.Deleted : PackageVersionResultType.Available;
            IsListed = entity.IsListed;
            IsSemVer2 = entity.SemVerType != null ? entity.GetSemVerType().Value.IsSemVer2() : null;
            SemVerType = entity.GetSemVerType();
        }

        public PackageVersionResultType ResultType { get; set; }

        public bool? IsListed { get; set; }
        public bool? IsSemVer2 { get; set; }
        public SemVerType? SemVerType { get; set; }

        public bool IsLatest { get; set; }
        public bool IsLatestStable { get; set; }
        public bool IsLatestSemVer2 { get; set; }
        public bool IsLatestStableSemVer2 { get; set; }
    }
}

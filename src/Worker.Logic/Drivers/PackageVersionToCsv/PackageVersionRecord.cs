// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;
using NuGet.Insights.Worker.LoadPackageVersion;

namespace NuGet.Insights.Worker.PackageVersionToCsv
{
    [CsvRecord]
    public partial record PackageVersionRecord : IPackageRecord, IAggregatedCsvRecord<PackageVersionRecord>
    {
        public PackageVersionRecord()
        {
        }

        public PackageVersionRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageVersionEntity entity)
        {
            this.Initialize(scanId, scanTimestamp, entity.PackageId, entity.PackageVersion, entity.CommitTimestamp, entity.Created);

            ResultType = entity.LeafType == CatalogLeafType.PackageDelete ? PackageVersionResultType.Deleted : PackageVersionResultType.Available;
            IsListed = entity.IsListed;
            IsSemVer2 = entity.SemVerType?.IsSemVer2();
            SemVerType = entity.SemVerType;

            NuGetVersion parsedVersion;
            if (entity.OriginalVersion != null)
            {
                OriginalVersion = entity.OriginalVersion;
                parsedVersion = NuGetVersion.Parse(entity.OriginalVersion);
                FullVersion = parsedVersion.ToFullString();
            }
            else
            {
                parsedVersion = NuGetVersion.Parse(entity.PackageVersion);
            }

            Major = parsedVersion.Major;
            Minor = parsedVersion.Minor;
            Patch = parsedVersion.Patch;
            Revision = parsedVersion.Revision;
            Release = parsedVersion.Release;
            ReleaseLabels = KustoDynamicSerializer.Serialize(parsedVersion.ReleaseLabels.ToList());
            Metadata = parsedVersion.Metadata;
            IsPrerelease = parsedVersion.IsPrerelease;

            Published = entity.Published;
            LastEdited = entity.LastEdited;
        }

        [KustoIgnore]
        public Guid? ScanId { get; set; }

        [KustoIgnore]
        public DateTimeOffset? ScanTimestamp { get; set; }

        [BucketKey]
        public string LowerId { get; set; }

        [KustoPartitionKey]
        public string Identity { get; set; }

        public string Id { get; set; }
        public string Version { get; set; }

        [Required]
        public DateTimeOffset CatalogCommitTimestamp { get; set; }

        public DateTimeOffset? Created { get; set; }

        [Required]
        public PackageVersionResultType ResultType { get; set; }

        public string OriginalVersion { get; set; }
        public string FullVersion { get; set; }

        [Required]
        public int Major { get; set; }

        [Required]
        public int Minor { get; set; }

        [Required]
        public int Patch { get; set; }

        [Required]
        public int Revision { get; set; }

        public string Release { get; set; }

        [KustoType("dynamic")]
        public string ReleaseLabels { get; set; }

        public string Metadata { get; set; }

        [Required]
        public bool IsPrerelease { get; set; }

        public bool? IsListed { get; set; }
        public bool? IsSemVer2 { get; set; }
        public SemVerType? SemVerType { get; set; }

        [Required]
        public int SemVerOrder { get; set; }

        [Required]
        public bool IsLatest { get; set; }

        [Required]
        public bool IsLatestStable { get; set; }

        [Required]
        public bool IsLatestSemVer2 { get; set; }

        [Required]
        public bool IsLatestStableSemVer2 { get; set; }

        public DateTimeOffset? Published { get; set; }
        public DateTimeOffset? LastEdited { get; set; }

        public static string CsvCompactMessageSchemaName => "cc.pv";

        public static IEqualityComparer<PackageVersionRecord> KeyComparer { get; } = PackageRecordIdentityComparer<PackageVersionRecord>.Instance;

        public static IReadOnlyList<string> KeyFields { get; } = PackageRecordExtensions.IdentityKeyField;

        public static List<PackageVersionRecord> Prune(List<PackageVersionRecord> records, bool isFinalPrune, IOptions<NuGetInsightsWorkerSettings> options, ILogger logger)
        {
            return PackageRecordExtensions.Prune(records, isFinalPrune);
        }

        public int CompareTo(PackageVersionRecord other)
        {
            return PackageRecordExtensions.CompareTo(this, other);
        }
    }
}

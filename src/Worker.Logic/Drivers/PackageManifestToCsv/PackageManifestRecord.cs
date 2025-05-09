// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace NuGet.Insights.Worker.PackageManifestToCsv
{
    [CsvRecord]
    public partial record PackageManifestRecord : PackageRecord, IAggregatedCsvRecord<PackageManifestRecord>
    {
        public PackageManifestRecord()
        {
        }

        public PackageManifestRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = PackageManifestRecordResultType.Deleted;
        }

        public PackageManifestRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = PackageManifestRecordResultType.Available;
        }

        [Required]
        public PackageManifestRecordResultType ResultType { get; set; }

        public int? Size { get; set; }

        public string OriginalId { get; set; }
        public string OriginalVersion { get; set; }

        public string MinClientVersion { get; set; }
        public bool? DevelopmentDependency { get; set; }
        public bool? IsServiceable { get; set; }

        public string Authors { get; set; }
        public string Copyright { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
        public string IconUrl { get; set; }
        public string Language { get; set; }
        public string LicenseUrl { get; set; }
        public string Owners { get; set; }
        public string ProjectUrl { get; set; }
        public string Readme { get; set; }
        public string ReleaseNotes { get; set; }
        public bool? RequireLicenseAcceptance { get; set; }
        public string Summary { get; set; }
        public string Tags { get; set; }
        public string Title { get; set; }

        [KustoType("dynamic")]
        public string PackageTypes { get; set; }

        [KustoType("dynamic")]
        public string LicenseMetadata { get; set; }

        [KustoType("dynamic")]
        public string RepositoryMetadata { get; set; }

        [KustoType("dynamic")]
        public string ReferenceGroups { get; set; }

        [KustoType("dynamic")]
        public string ContentFiles { get; set; }

        [KustoType("dynamic")]
        public string DependencyGroups { get; set; }

        [KustoType("dynamic")]
        public string FrameworkAssemblyGroups { get; set; }

        [KustoType("dynamic")]
        public string FrameworkRefGroups { get; set; }

        public bool? ContentFilesHasFormatException { get; set; }
        public bool? DependencyGroupsHasMissingId { get; set; }

        [KustoType("dynamic")]
        public string SplitTags { get; set; }

        public static string CsvCompactMessageSchemaName => "cc.pm";
        public static IEqualityComparer<PackageManifestRecord> KeyComparer { get; } = PackageRecordIdentityComparer<PackageManifestRecord>.Instance;
        public static IReadOnlyList<string> KeyFields { get; } = PackageRecordExtensions.IdentityKeyField;

        public static List<PackageManifestRecord> Prune(List<PackageManifestRecord> records, bool isFinalPrune, IOptions<NuGetInsightsWorkerSettings> options, ILogger logger)
        {
            return PackageRecordExtensions.Prune(records, isFinalPrune);
        }

        public int CompareTo(PackageManifestRecord other)
        {
            return base.CompareTo(other);
        }
    }
}

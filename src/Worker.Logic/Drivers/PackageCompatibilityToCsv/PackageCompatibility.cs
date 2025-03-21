// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace NuGet.Insights.Worker.PackageCompatibilityToCsv
{
    [CsvRecord]
    public partial record PackageCompatibility : PackageRecord, IAggregatedCsvRecord<PackageCompatibility>
    {
        public PackageCompatibility()
        {
        }

        public PackageCompatibility(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf) : base(scanId, scanTimestamp, leaf)
        {
            ResultType = PackageCompatibilityResultType.Deleted;
        }

        public PackageCompatibility(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf) : base(scanId, scanTimestamp, leaf)
        {
            ResultType = PackageCompatibilityResultType.Available;
        }

        [Required]
        public PackageCompatibilityResultType ResultType { get; set; }

        public bool? HasError { get; set; }
        public bool? DoesNotRoundTrip { get; set; }
        public bool? HasAny { get; set; }
        public bool? HasUnsupported { get; set; }
        public bool? HasAgnostic { get; set; }

        [KustoType("dynamic")]
        public string BrokenFrameworks { get; set; }

        [KustoType("dynamic")]
        public string NuspecReader { get; set; }

        [KustoType("dynamic")]
        public string NU1202 { get; set; }

        [KustoType("dynamic")]
        public string NuGetGallery { get; set; }

        [KustoType("dynamic")]
        public string NuGetGalleryEscaped { get; set; }

        [KustoType("dynamic")]
        public string NuGetGallerySupported { get; set; }

        [KustoType("dynamic")]
        public string NuGetGalleryBadges { get; set; }

        public static string CsvCompactMessageSchemaName => "cc.pco";

        public static IEqualityComparer<PackageCompatibility> KeyComparer { get; } = PackageRecordIdentityComparer<PackageCompatibility>.Instance;

        public static IReadOnlyList<string> KeyFields { get; } = PackageRecordExtensions.IdentityKeyField;

        public static List<PackageCompatibility> Prune(List<PackageCompatibility> records, bool isFinalPrune, IOptions<NuGetInsightsWorkerSettings> options, ILogger logger)
        {
            return PackageRecordExtensions.Prune(records, isFinalPrune);
        }

        public int CompareTo(PackageCompatibility other)
        {
            return base.CompareTo(other);
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace NuGet.Insights.Worker.PackageReadmeToCsv
{
    [CsvRecord]
    public partial record PackageReadme : PackageRecord, IAggregatedCsvRecord<PackageReadme>
    {
        public PackageReadme()
        {
        }

        public PackageReadme(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = PackageReadmeResultType.Deleted;
        }

        public PackageReadme(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = PackageReadmeResultType.None;
        }

        [Required]
        public PackageReadmeResultType ResultType { get; set; }

        public int? Size { get; set; }
        public DateTimeOffset? LastModified { get; set; }
        public string SHA256 { get; set; }
        public string Content { get; set; }

        public static string CsvCompactMessageSchemaName => "cc.pr";

        public static IEqualityComparer<PackageReadme> KeyComparer { get; } = PackageRecordIdentityComparer<PackageReadme>.Instance;

        public static IReadOnlyList<string> KeyFields { get; } = PackageRecordExtensions.IdentityKeyField;

        public static List<PackageReadme> Prune(List<PackageReadme> records, bool isFinalPrune, IOptions<NuGetInsightsWorkerSettings> options, ILogger logger)
        {
            return PackageRecordExtensions.Prune(records, isFinalPrune);
        }

        public int CompareTo(PackageReadme other)
        {
            return base.CompareTo(other);
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace NuGet.Insights.Worker.CatalogDataToCsv
{
    [CsvRecord]
    public partial record PackageDeprecationRecord : PackageRecord, IAggregatedCsvRecord<PackageDeprecationRecord>
    {
        public PackageDeprecationRecord()
        {
        }

        public PackageDeprecationRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf) : base(scanId, scanTimestamp, leaf)
        {
            ResultType = PackageDeprecationResultType.Deleted;
        }

        public PackageDeprecationRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf) : base(scanId, scanTimestamp, leaf)
        {
        }

        [Required]
        public PackageDeprecationResultType ResultType { get; set; }

        public string Message { get; set; }

        [KustoType("dynamic")]
        public string Reasons { get; set; }

        public string AlternatePackageId { get; set; }
        public string AlternateVersionRange { get; set; }

        public static string CsvCompactMessageSchemaName => "cc.pd";

        public static IEqualityComparer<PackageDeprecationRecord> KeyComparer { get; } = PackageRecordIdentityComparer<PackageDeprecationRecord>.Instance;

        public static IReadOnlyList<string> KeyFields { get; } = PackageRecordExtensions.IdentityKeyField;

        public static List<PackageDeprecationRecord> Prune(List<PackageDeprecationRecord> records, bool isFinalPrune, IOptions<NuGetInsightsWorkerSettings> options, ILogger logger)
        {
            return PackageRecordExtensions.Prune(records, isFinalPrune);
        }

        public int CompareTo(PackageDeprecationRecord other)
        {
            return base.CompareTo(other);
        }
    }
}

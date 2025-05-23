// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace NuGet.Insights.Worker.PackageLicenseToCsv
{
    [CsvRecord]
    public partial record PackageLicense : PackageRecord, IAggregatedCsvRecord<PackageLicense>
    {
        public PackageLicense()
        {
        }

        public PackageLicense(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = PackageLicenseResultType.Deleted;
        }

        public PackageLicense(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = PackageLicenseResultType.None;
        }

        [Required]
        public PackageLicenseResultType ResultType { get; set; }

        public string Url { get; set; }
        public string Expression { get; set; }
        public string File { get; set; }

        public string GeneratedUrl { get; set; }

        [KustoType("dynamic")]
        public string ExpressionParsed { get; set; }
        [KustoType("dynamic")]
        public string ExpressionLicenses { get; set; }
        [KustoType("dynamic")]
        public string ExpressionExceptions { get; set; }
        [KustoType("dynamic")]
        public string ExpressionNonStandardLicenses { get; set; }
        public bool? ExpressionHasDeprecatedIdentifier { get; set; }

        public long? FileLength { get; set; }
        public string FileSHA256 { get; set; }
        public string FileContent { get; set; }

        public static string CsvCompactMessageSchemaName => "cc.pl";

        public static IEqualityComparer<PackageLicense> KeyComparer { get; } = PackageRecordIdentityComparer<PackageLicense>.Instance;

        public static IReadOnlyList<string> KeyFields { get; } = PackageRecordExtensions.IdentityKeyField;

        public static List<PackageLicense> Prune(List<PackageLicense> records, bool isFinalPrune, IOptions<NuGetInsightsWorkerSettings> options, ILogger logger)
        {
            return PackageRecordExtensions.Prune(records, isFinalPrune);
        }

        public int CompareTo(PackageLicense other)
        {
            return base.CompareTo(other);
        }
    }
}

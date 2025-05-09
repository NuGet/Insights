// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;
using NuGetPe;

namespace NuGet.Insights.Worker.NuGetPackageExplorerToCsv
{
    [CsvRecord]
    public partial record NuGetPackageExplorerRecord : PackageRecord, IAggregatedCsvRecord<NuGetPackageExplorerRecord>
    {
        public NuGetPackageExplorerRecord()
        {
        }

        public NuGetPackageExplorerRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = NuGetPackageExplorerResultType.Deleted;
        }

        public NuGetPackageExplorerRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = NuGetPackageExplorerResultType.Available;
        }

        [Required]
        public NuGetPackageExplorerResultType ResultType { get; set; }

        public SymbolValidationResult? SourceLinkResult { get; set; }
        public DeterministicResult? DeterministicResult { get; set; }
        public HasCompilerFlagsResult? CompilerFlagsResult { get; set; }
        public bool? IsSignedByAuthor { get; set; }

        public static string CsvCompactMessageSchemaName => "cc.npe";

        public static IEqualityComparer<NuGetPackageExplorerRecord> KeyComparer { get; } = PackageRecordIdentityComparer<NuGetPackageExplorerRecord>.Instance;

        public static IReadOnlyList<string> KeyFields { get; } = PackageRecordExtensions.IdentityKeyField;

        public static List<NuGetPackageExplorerRecord> Prune(List<NuGetPackageExplorerRecord> records, bool isFinalPrune, IOptions<NuGetInsightsWorkerSettings> options, ILogger logger)
        {
            return PackageRecordExtensions.Prune(records, isFinalPrune);
        }

        public int CompareTo(NuGetPackageExplorerRecord other)
        {
            return base.CompareTo(other);
        }
    }
}

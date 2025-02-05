// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace NuGet.Insights.Worker.PackageAssemblyToCsv
{
    [CsvRecord]
    public partial record PackageAssembly : PackageRecord, IAggregatedCsvRecord<PackageAssembly>, IPackageEntryRecord
    {
        public PackageAssembly()
        {
        }

        public PackageAssembly(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = PackageAssemblyResultType.Deleted;
        }

        public PackageAssembly(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf, PackageAssemblyResultType resultType)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = resultType;
        }

        [Required]
        public PackageAssemblyResultType ResultType { get; set; }

        public int? SequenceNumber { get; set; }

        public string Path { get; set; }
        public string FileName { get; set; }
        public string FileExtension { get; set; }
        public string TopLevelFolder { get; set; }

        public long? FileLength { get; set; }

        public PackageAssemblyEdgeCases? EdgeCases { get; set; }
        public string AssemblyName { get; set; }
        public Version AssemblyVersion { get; set; }
        public string Culture { get; set; }

        public string PublicKeyToken { get; set; }

        public AssemblyHashAlgorithm? HashAlgorithm { get; set; }

        public bool? HasPublicKey { get; set; }
        public int? PublicKeyLength { get; set; }
        public string PublicKeySHA1 { get; set; }

        [KustoType("dynamic")]
        public string CustomAttributes { get; set; }

        [KustoType("dynamic")]
        public string CustomAttributesFailedDecode { get; set; }

        public int? CustomAttributesTotalCount { get; set; }
        public int? CustomAttributesTotalDataLength { get; set; }

        public static string CsvCompactMessageSchemaName => "cc.as";

        public static IEqualityComparer<PackageAssembly> KeyComparer { get; } = PackageEntryKeyComparer<PackageAssembly>.Instance;

        public static IReadOnlyList<string> KeyFields { get; } = PackageRecordExtensions.PackageEntryKeyFields;

        public static List<PackageAssembly> Prune(List<PackageAssembly> records, bool isFinalPrune, IOptions<NuGetInsightsWorkerSettings> options, ILogger logger)
        {
            return PackageRecordExtensions.Prune(records, isFinalPrune);
        }

        public int CompareTo(PackageAssembly other)
        {
            var c = base.CompareTo(other);
            if (c != 0)
            {
                return c;
            }

            return Comparer<int?>.Default.Compare(SequenceNumber, other.SequenceNumber);
        }
    }
}

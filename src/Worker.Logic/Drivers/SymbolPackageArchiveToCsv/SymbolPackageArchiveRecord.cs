// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.SymbolPackageArchiveToCsv
{
    public partial record SymbolPackageArchiveRecord : ArchiveRecord, IAggregatedCsvRecord<SymbolPackageArchiveRecord>
    {
        public SymbolPackageArchiveRecord()
        {
        }

        public SymbolPackageArchiveRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
        }

        public SymbolPackageArchiveRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
        }

        public static string CsvCompactMessageSchemaName => "cc.spa";

        public static IEqualityComparer<SymbolPackageArchiveRecord> GetKeyComparer() => IdentityComparer<SymbolPackageArchiveRecord>.Instance;

        public static IReadOnlyList<string> KeyFields { get; } = IdentityKeyField;

        public static List<SymbolPackageArchiveRecord> Prune(List<SymbolPackageArchiveRecord> records, bool isFinalPrune, IOptions<NuGetInsightsWorkerSettings> options, ILogger logger)
        {
            return Prune(records, isFinalPrune);
        }

        public int CompareTo(SymbolPackageArchiveRecord other)
        {
            return base.CompareTo(other);
        }

        public string GetBucketKey()
        {
            return Identity;
        }
    }
}

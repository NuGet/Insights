// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.SymbolPackageArchiveToCsv
{
    [CsvRecord]
    public partial record SymbolPackageArchiveEntry : ArchiveEntry, IAggregatedCsvRecord<SymbolPackageArchiveEntry>
    {
        public SymbolPackageArchiveEntry()
        {
        }

        public SymbolPackageArchiveEntry(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
        }

        public SymbolPackageArchiveEntry(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
        }

        public static string CsvCompactMessageSchemaName => "cc.spae";

        public static IEqualityComparer<SymbolPackageArchiveEntry> KeyComparer { get; } = PackageEntryKeyComparer<SymbolPackageArchiveEntry>.Instance;

        public static IReadOnlyList<string> KeyFields { get; } = PackageRecordExtensions.PackageEntryKeyFields;

        public static List<SymbolPackageArchiveEntry> Prune(List<SymbolPackageArchiveEntry> records, bool isFinalPrune, IOptions<NuGetInsightsWorkerSettings> options, ILogger logger)
        {
            return PackageRecordExtensions.Prune(records, isFinalPrune);
        }

        public int CompareTo(SymbolPackageArchiveEntry other)
        {
            return base.CompareTo(other);
        }
    }
}

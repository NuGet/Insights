// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.PackageArchiveToCsv
{
    [CsvRecord]
    public partial record PackageArchiveEntry : ArchiveEntry, IAggregatedCsvRecord<PackageArchiveEntry>
    {
        public PackageArchiveEntry()
        {
        }

        public PackageArchiveEntry(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
        }

        public PackageArchiveEntry(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
        }

        public static string CsvCompactMessageSchemaName => "cc.pae";

        public static IEqualityComparer<PackageArchiveEntry> KeyComparer { get; } = PackageEntryKeyComparer<PackageArchiveEntry>.Instance;

        public static IReadOnlyList<string> KeyFields { get; } = PackageRecordExtensions.PackageEntryKeyFields;

        public static List<PackageArchiveEntry> Prune(List<PackageArchiveEntry> records, bool isFinalPrune, IOptions<NuGetInsightsWorkerSettings> options, ILogger logger)
        {
            return PackageRecordExtensions.Prune(records, isFinalPrune);
        }

        public int CompareTo(PackageArchiveEntry other)
        {
            return base.CompareTo(other);
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.PackageArchiveToCsv
{
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

        public static IEqualityComparer<PackageArchiveEntry> KeyComparer { get; } = IPackageEntryRecord.PackageEntryKeyComparer<PackageArchiveEntry>.Instance;

        public static IReadOnlyList<string> KeyFields { get; } = PackageEntryKeyFields;

        public static List<PackageArchiveEntry> Prune(List<PackageArchiveEntry> records, bool isFinalPrune, IOptions<NuGetInsightsWorkerSettings> options, ILogger logger)
        {
            return Prune(records, isFinalPrune);
        }

        public int CompareTo(PackageArchiveEntry other)
        {
            var c = base.CompareTo(other);
            if (c != 0)
            {
                return c;
            }

            return Comparer<int?>.Default.Compare(SequenceNumber, other.SequenceNumber);
        }

        public string GetBucketKey()
        {
            return Identity;
        }
    }
}

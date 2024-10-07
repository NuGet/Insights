// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.PackageArchiveToCsv
{
    public partial record PackageArchiveRecord : ArchiveRecord, IAggregatedCsvRecord<PackageArchiveRecord>
    {
        public PackageArchiveRecord()
        {
        }

        public PackageArchiveRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
        }

        public PackageArchiveRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
        }

        public static string CsvCompactMessageSchemaName => "cc.pa";

        public static IEqualityComparer<PackageArchiveRecord> KeyComparer { get; } = IdentityComparer<PackageArchiveRecord>.Instance;

        public static IReadOnlyList<string> KeyFields { get; } = IdentityKeyField;

        public static List<PackageArchiveRecord> Prune(List<PackageArchiveRecord> records, bool isFinalPrune, IOptions<NuGetInsightsWorkerSettings> options, ILogger logger)
        {
            return Prune(records, isFinalPrune);
        }

        public int CompareTo(PackageArchiveRecord other)
        {
            return base.CompareTo(other);
        }

        public string GetBucketKey()
        {
            return Identity;
        }
    }
}

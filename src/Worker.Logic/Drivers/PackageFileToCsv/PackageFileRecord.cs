// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.PackageFileToCsv
{
    [CsvRecord]
    public partial record PackageFileRecord : FileRecord, IAggregatedCsvRecord<PackageFileRecord>
    {
        public PackageFileRecord()
        {
        }

        public PackageFileRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf) : base(scanId, scanTimestamp, leaf)
        {
        }

        public PackageFileRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf) : base(scanId, scanTimestamp, leaf)
        {
        }

        public static string CsvCompactMessageSchemaName => "cc.pf";

        public static IEqualityComparer<PackageFileRecord> KeyComparer { get; } = PackageEntryKeyComparer<PackageFileRecord>.Instance;

        public static IReadOnlyList<string> KeyFields { get; } = PackageRecordExtensions.PackageEntryKeyFields;

        public static List<PackageFileRecord> Prune(List<PackageFileRecord> records, bool isFinalPrune, IOptions<NuGetInsightsWorkerSettings> options, ILogger logger)
        {
            return PackageRecordExtensions.Prune(records, isFinalPrune);
        }

        public int CompareTo(PackageFileRecord other)
        {
            return base.CompareTo(other);
        }
    }
}

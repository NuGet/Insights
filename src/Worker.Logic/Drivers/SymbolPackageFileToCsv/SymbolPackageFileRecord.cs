// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.SymbolPackageFileToCsv
{
    public partial record SymbolPackageFileRecord : FileRecord, IAggregatedCsvRecord<SymbolPackageFileRecord>
    {
        public SymbolPackageFileRecord()
        {
        }

        public SymbolPackageFileRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf) : base(scanId, scanTimestamp, leaf)
        {
        }

        public SymbolPackageFileRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf) : base(scanId, scanTimestamp, leaf)
        {
        }

        public static string CsvCompactMessageSchemaName => "cc.spf";

        public static IEqualityComparer<SymbolPackageFileRecord> KeyComparer { get; } = PackageEntryKeyComparer<SymbolPackageFileRecord>.Instance;

        public static IReadOnlyList<string> KeyFields { get; } = PackageRecordExtensions.PackageEntryKeyFields;

        public static List<SymbolPackageFileRecord> Prune(List<SymbolPackageFileRecord> records, bool isFinalPrune, IOptions<NuGetInsightsWorkerSettings> options, ILogger logger)
        {
            return PackageRecordExtensions.Prune(records, isFinalPrune);
        }

        public int CompareTo(SymbolPackageFileRecord other)
        {
            return base.CompareTo(other);
        }
    }
}

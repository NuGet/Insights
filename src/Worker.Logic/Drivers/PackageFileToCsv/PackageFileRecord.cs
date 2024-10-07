// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.PackageFileToCsv
{
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

        public static string GetCsvCompactMessageSchemaName() => "cc.pf";

        public static IEqualityComparer<PackageFileRecord> GetKeyComparer() => IPackageEntryRecord.PackageEntryKeyComparer<PackageFileRecord>.Instance;

        public static IReadOnlyList<string> KeyFields { get; } = PackageEntryKeyFields;

        public static List<PackageFileRecord> Prune(List<PackageFileRecord> records, bool isFinalPrune, IOptions<NuGetInsightsWorkerSettings> options, ILogger logger)
        {
            return Prune(records, isFinalPrune);
        }

        public int CompareTo(PackageFileRecord other)
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

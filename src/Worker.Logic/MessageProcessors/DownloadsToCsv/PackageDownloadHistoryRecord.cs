// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.DownloadsToCsv
{
    [CsvRecord]
    [NoKustoDDL]
    public partial record PackageDownloadHistoryRecord : IPackageDownloadRecord<PackageDownloadHistoryRecord>
    {
        public DateTimeOffset AsOfTimestamp { get; set; }

        public string LowerId { get; set; }

        [BucketKey]
        [KustoPartitionKey]
        public string Identity { get; set; }

        public string Id { get; set; }
        public string Version { get; set; }
        public long Downloads { get; set; }
        public long TotalDownloads { get; set; }

        public static IEqualityComparer<PackageDownloadHistoryRecord> KeyComparer => PackageDownloadHistoryRecordKeyComparer.Instance;
        public static IReadOnlyList<string> KeyFields { get; } = [nameof(Identity), nameof(AsOfTimestamp)];

        public int CompareTo(PackageDownloadHistoryRecord other)
        {
            var c = PackageRecordExtensions.CompareTo(LowerId, Identity, other.LowerId, other.Identity);
            if (c != 0)
            {
                return c;
            }

            return AsOfTimestamp.CompareTo(other.AsOfTimestamp);
        }

        public static List<PackageDownloadHistoryRecord> Prune(
            List<PackageDownloadHistoryRecord> records,
            bool isFinalPrune,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger logger)
        {
            return records
                .GroupBy(x => (x.Identity, x.AsOfTimestamp))
                .Select(g => g.MaxBy(x => x.Downloads))
                .Order()
                .ToList();
        }

        public class PackageDownloadHistoryRecordKeyComparer : IEqualityComparer<PackageDownloadHistoryRecord>
        {
            public static PackageDownloadHistoryRecordKeyComparer Instance { get; } = new PackageDownloadHistoryRecordKeyComparer();

            public bool Equals(PackageDownloadHistoryRecord x, PackageDownloadHistoryRecord y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (x is null || y is null)
                {
                    return false;
                }

                return x.Identity == y.Identity
                    && x.AsOfTimestamp == y.AsOfTimestamp;
            }

            public int GetHashCode([DisallowNull] PackageDownloadHistoryRecord obj)
            {
                return HashCode.Combine(obj.Identity, obj.AsOfTimestamp);
            }
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.DownloadsToCsv
{
    [NoKustoDDL]
    public partial record PackageDownloadHistoryRecord : IPackageDownloadRecord<PackageDownloadHistoryRecord>
    {
        public DateTimeOffset AsOfTimestamp { get; set; }

        public string LowerId { get; set; }

        [KustoPartitionKey]
        public string Identity { get; set; }

        public string Id { get; set; }
        public string Version { get; set; }
        public long Downloads { get; set; }
        public long TotalDownloads { get; set; }

        public static IEqualityComparer<PackageDownloadHistoryRecord> KeyComparer => PackageDownloadHistoryRecordKeyComparer.Instance;
        public static IReadOnlyList<string> KeyFields { get; } = [nameof(Identity)];

        public int CompareTo(PackageDownloadHistoryRecord other)
        {
            return string.CompareOrdinal(Identity, other.Identity);
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

                return x.Identity == y.Identity;
            }

            public int GetHashCode([DisallowNull] PackageDownloadHistoryRecord obj)
            {
                return obj.Identity.GetHashCode(StringComparison.Ordinal);
            }
        }
    }
}

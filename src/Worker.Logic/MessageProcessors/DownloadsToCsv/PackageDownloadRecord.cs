// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace NuGet.Insights.Worker.DownloadsToCsv
{
    public partial record PackageDownloadRecord : IPackageDownloadRecord<PackageDownloadRecord>
    {
        [KustoIgnore]
        public DateTimeOffset AsOfTimestamp { get; set; }

        public string LowerId { get; set; }

        [KustoPartitionKey]
        public string Identity { get; set; }

        public string Id { get; set; }
        public string Version { get; set; }

        [Required]
        public long Downloads { get; set; }

        [Required]
        public long TotalDownloads { get; set; }

        public string GetBucketKey() => LowerId;
        public static IEqualityComparer<PackageDownloadRecord> KeyComparer => PackageDownloadRecordKeyComparer.Instance;
        public static IReadOnlyList<string> KeyFields { get; } = [nameof(Identity)];

        public int CompareTo(PackageDownloadRecord other)
        {
            return string.CompareOrdinal(Identity, other.Identity);
        }

        public class PackageDownloadRecordKeyComparer : IEqualityComparer<PackageDownloadRecord>
        {
            public static PackageDownloadRecordKeyComparer Instance { get; } = new PackageDownloadRecordKeyComparer();

            public bool Equals(PackageDownloadRecord x, PackageDownloadRecord y)
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

            public int GetHashCode([DisallowNull] PackageDownloadRecord obj)
            {
                return obj.Identity.GetHashCode(StringComparison.Ordinal);
            }
        }
    }
}

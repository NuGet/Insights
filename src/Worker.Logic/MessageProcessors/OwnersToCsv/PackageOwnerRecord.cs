// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.OwnersToCsv
{
    [CsvRecord]
    public partial record PackageOwnerRecord : IAuxiliaryFileCsvRecord<PackageOwnerRecord>
    {
        [KustoIgnore]
        public DateTimeOffset AsOfTimestamp { get; set; }

        [BucketKey]
        [KustoPartitionKey]
        public string LowerId { get; set; }

        public string Id { get; set; }

        [KustoType("dynamic")]
        public string Owners { get; set; }

        public static IEqualityComparer<PackageOwnerRecord> KeyComparer => PackageOwnerRecordKeyComparer.Instance;
        public static IReadOnlyList<string> KeyFields { get; } = [nameof(LowerId)];

        public int CompareTo(PackageOwnerRecord other)
        {
            return string.CompareOrdinal(LowerId, other.LowerId);
        }

        public static List<PackageOwnerRecord> Prune(
            List<PackageOwnerRecord> records,
            bool isFinalPrune,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger logger)
        {
            // no duplicates are expected
            return records.Order().ToList();
        }

        public class PackageOwnerRecordKeyComparer : IEqualityComparer<PackageOwnerRecord>
        {
            public static PackageOwnerRecordKeyComparer Instance { get; } = new PackageOwnerRecordKeyComparer();

            public bool Equals(PackageOwnerRecord x, PackageOwnerRecord y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (x is null || y is null)
                {
                    return false;
                }

                return x.LowerId == y.LowerId;
            }

            public int GetHashCode([DisallowNull] PackageOwnerRecord obj)
            {
                return obj.LowerId.GetHashCode(StringComparison.Ordinal);
            }
        }
    }
}

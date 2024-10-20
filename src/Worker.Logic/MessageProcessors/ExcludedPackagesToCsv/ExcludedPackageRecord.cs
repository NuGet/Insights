// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace NuGet.Insights.Worker.ExcludedPackagesToCsv
{
    public partial record ExcludedPackageRecord : IAuxiliaryFileCsvRecord<ExcludedPackageRecord>
    {
        [KustoIgnore]
        public DateTimeOffset AsOfTimestamp { get; set; }

        [BucketKey]
        [KustoPartitionKey]
        public string LowerId { get; set; }

        public string Id { get; set; }

        [Required]
        public bool IsExcluded { get; set; }

        public static IEqualityComparer<ExcludedPackageRecord> KeyComparer => ExcludedPackageRecordKeyComparer.Instance;
        public static IReadOnlyList<string> KeyFields { get; } = [nameof(LowerId)];

        public int CompareTo(ExcludedPackageRecord other)
        {
            return string.CompareOrdinal(LowerId, other.LowerId);
        }

        public class ExcludedPackageRecordKeyComparer : IEqualityComparer<ExcludedPackageRecord>
        {
            public static ExcludedPackageRecordKeyComparer Instance { get; } = new ExcludedPackageRecordKeyComparer();

            public bool Equals(ExcludedPackageRecord x, ExcludedPackageRecord y)
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

            public int GetHashCode([DisallowNull] ExcludedPackageRecord obj)
            {
                return obj.LowerId.GetHashCode(StringComparison.Ordinal);
            }
        }
    }
}

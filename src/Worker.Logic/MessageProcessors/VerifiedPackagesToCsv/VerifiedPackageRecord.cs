// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace NuGet.Insights.Worker.VerifiedPackagesToCsv
{
    public partial record VerifiedPackageRecord : IAuxiliaryFileCsvRecord<VerifiedPackageRecord>
    {
        [KustoIgnore]
        public DateTimeOffset AsOfTimestamp { get; set; }

        [BucketKey]
        [KustoPartitionKey]
        public string LowerId { get; set; }

        public string Id { get; set; }

        [Required]
        public bool IsVerified { get; set; }

        public static IEqualityComparer<VerifiedPackageRecord> KeyComparer => VerifiedPackageRecordKeyComparer.Instance;
        public static IReadOnlyList<string> KeyFields { get; } = [nameof(LowerId)];

        public int CompareTo(VerifiedPackageRecord other)
        {
            return string.CompareOrdinal(LowerId, other.LowerId);
        }

        public class VerifiedPackageRecordKeyComparer : IEqualityComparer<VerifiedPackageRecord>
        {
            public static VerifiedPackageRecordKeyComparer Instance { get; } = new VerifiedPackageRecordKeyComparer();

            public bool Equals(VerifiedPackageRecord x, VerifiedPackageRecord y)
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

            public int GetHashCode([DisallowNull] VerifiedPackageRecord obj)
            {
                return obj.LowerId.GetHashCode(StringComparison.Ordinal);
            }
        }
    }
}

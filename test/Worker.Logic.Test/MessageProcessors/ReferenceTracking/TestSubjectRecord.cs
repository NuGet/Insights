// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace NuGet.Insights.Worker.ReferenceTracking
{
    [CsvRecord]
    public partial record TestSubjectRecord : IAggregatedCsvRecord<TestSubjectRecord>, ICleanupOrphanCsvRecord
    {
        [BucketKey]
        [KustoPartitionKey]
        public string BucketKey { get; set; }

        public string Id { get; set; }

        [Required]
        public bool IsOrphan { get; set; }

        public static string CsvCompactMessageSchemaName => "cc.ts";
        public static string CleanupOrphanRecordsMessageSchemaName => "co.ts";
        public static IEqualityComparer<TestSubjectRecord> KeyComparer { get; } = TestSubjectRecordKeyComparer.Instance;
        public static IReadOnlyList<string> KeyFields { get; } = [nameof(Id)];

        public static List<TestSubjectRecord> Prune(List<TestSubjectRecord> records, bool isFinalPrune, IOptions<NuGetInsightsWorkerSettings> options, ILogger logger)
        {
            return records
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .Where(x => !isFinalPrune || !x.IsOrphan)
                .Distinct()
                .Order()
                .ToList();
        }

        public int CompareTo(TestSubjectRecord other)
        {
            var c = string.CompareOrdinal(BucketKey, other.BucketKey);
            if (c != 0)
            {
                return c;
            }

            return string.CompareOrdinal(Id, other.Id);
        }

        public class TestSubjectRecordKeyComparer : IEqualityComparer<TestSubjectRecord>
        {
            public static TestSubjectRecordKeyComparer Instance { get; } = new();

            public bool Equals(TestSubjectRecord x, TestSubjectRecord y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (x is null || y is null)
                {
                    return false;
                }

                return x.Id == y.Id;
            }

            public int GetHashCode([DisallowNull] TestSubjectRecord obj)
            {
                return obj.Id.GetHashCode(StringComparison.Ordinal);
            }
        }
    }
}

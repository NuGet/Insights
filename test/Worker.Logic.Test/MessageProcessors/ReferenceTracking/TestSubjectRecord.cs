// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.ReferenceTracking
{
    public partial record TestSubjectRecord : ICsvRecord, IAggregatedCsvRecord<TestSubjectRecord>
    {
        [KustoPartitionKey]
        public string BucketKey { get; set; }

        public string Id { get; set; }

        public bool IsOrphan { get; set; }

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

        public string GetBucketKey()
        {
            return BucketKey;
        }
    }
}

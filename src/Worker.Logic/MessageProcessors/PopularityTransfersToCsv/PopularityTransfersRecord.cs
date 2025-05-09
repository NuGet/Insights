// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.PopularityTransfersToCsv
{
    [CsvRecord]
    public partial record PopularityTransfersRecord : IAuxiliaryFileCsvRecord<PopularityTransfersRecord>
    {
        [KustoIgnore]
        public DateTimeOffset AsOfTimestamp { get; set; }

        [BucketKey]
        [KustoPartitionKey]
        public string LowerId { get; set; }

        public string Id { get; set; }

        [KustoType("dynamic")]
        public string TransferIds { get; set; }

        [KustoType("dynamic")]
        public string TransferLowerIds { get; set; }

        public static IEqualityComparer<PopularityTransfersRecord> KeyComparer => PopularityTransfersRecordKeyComparer.Instance;
        public static IReadOnlyList<string> KeyFields { get; } = [nameof(LowerId)];

        public int CompareTo(PopularityTransfersRecord other)
        {
            return string.CompareOrdinal(LowerId, other.LowerId);
        }

        public static List<PopularityTransfersRecord> Prune(
            List<PopularityTransfersRecord> records,
            bool isFinalPrune,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger logger)
        {
            // no duplicates are expected
            return records.Order().ToList();
        }

        public class PopularityTransfersRecordKeyComparer : IEqualityComparer<PopularityTransfersRecord>
        {
            public static PopularityTransfersRecordKeyComparer Instance { get; } = new PopularityTransfersRecordKeyComparer();

            public bool Equals(PopularityTransfersRecord x, PopularityTransfersRecord y)
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

            public int GetHashCode([DisallowNull] PopularityTransfersRecord obj)
            {
                return obj.LowerId.GetHashCode(StringComparison.Ordinal);
            }
        }
    }
}

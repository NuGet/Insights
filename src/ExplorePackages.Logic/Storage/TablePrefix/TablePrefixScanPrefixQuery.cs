using System;
using System.Diagnostics;

namespace Knapcode.ExplorePackages
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public class TablePrefixScanPrefixQuery : TablePrefixScanStep
    {
        public TablePrefixScanPrefixQuery(TableQueryParameters parameters, int depth, string partitionKeyPrefix, string partitionKeyLowerBound)
            : base(parameters, depth)
        {
            PartitionKeyPrefix = partitionKeyPrefix ?? throw new ArgumentNullException(nameof(partitionKeyPrefix));
            PartitionKeyLowerBound = partitionKeyLowerBound ?? throw new ArgumentNullException(nameof(partitionKeyLowerBound));
        }

        public override string DebuggerDisplay => $"prefix query PK = '{PartitionKeyPrefix}*', PK > '{PartitionKeyLowerBound}'";

        public string PartitionKeyPrefix { get; }
        public string PartitionKeyLowerBound { get; }
    }
}

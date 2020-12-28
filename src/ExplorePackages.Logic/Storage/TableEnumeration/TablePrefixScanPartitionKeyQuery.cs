using System;
using System.Diagnostics;

namespace Knapcode.ExplorePackages
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public class TablePrefixScanPartitionKeyQuery : TablePrefixScanStep
    {
        public TablePrefixScanPartitionKeyQuery(TableQueryParameters parameters, int depth, string partitionKey, string rowKeySkip)
            : base(parameters, depth)
        {
            PartitionKey = partitionKey ?? throw new ArgumentNullException(nameof(partitionKey));
            RowKeySkip = rowKeySkip ?? throw new ArgumentNullException(rowKeySkip);
        }

        public override string DebuggerDisplay => $"Partition key query: PK = '{PartitionKey}', RK > '{RowKeySkip}'";

        public string PartitionKey { get; }
        public string RowKeySkip { get; }
    }
}

using System;
using System.Diagnostics;

namespace Knapcode.ExplorePackages
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public class TablePrefixScanPartitionKeyStep : TablePrefixScanResult
    {
        public TablePrefixScanPartitionKeyStep(TableQueryParameters parameters, int depth, string partitionKey, string rowKeySkip)
            : base(parameters, depth)
        {
            PartitionKey = partitionKey ?? throw new ArgumentNullException(nameof(partitionKey));
            RowKeySkip = rowKeySkip ?? throw new ArgumentNullException(rowKeySkip);
        }

        public override string DebuggerDisplay => $"Enumerate query: PK = '{PartitionKey}', RK > '{RowKeySkip}'";

        public string PartitionKey { get; }
        public string RowKeySkip { get; }
    }
}

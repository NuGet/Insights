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
            RowKeySkip = rowKeySkip;
        }

        public override string DebuggerDisplay
        {
            get
            {
                var output = $"Partition key query: PK = '{PartitionKey}'";
                if (RowKeySkip != null)
                {
                    output += ", RK > '{RowKeySkip}'";
                }

                return output;
            }
        }

        public string PartitionKey { get; }
        public string RowKeySkip { get; }
    }
}

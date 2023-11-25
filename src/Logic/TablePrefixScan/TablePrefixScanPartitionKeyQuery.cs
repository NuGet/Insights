// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.TablePrefixScan
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
                var output = $"partition key query PK = '{PartitionKey}'";
                if (RowKeySkip != null)
                {
                    output += $", RK > '{RowKeySkip}'";
                }

                return output;
            }
        }

        public string PartitionKey { get; }
        public string RowKeySkip { get; }
    }
}

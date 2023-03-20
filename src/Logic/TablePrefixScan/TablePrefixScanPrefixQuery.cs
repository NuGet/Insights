// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;

namespace NuGet.Insights.TablePrefixScan
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public class TablePrefixScanPrefixQuery : TablePrefixScanStep
    {
        public TablePrefixScanPrefixQuery(
            TableQueryParameters parameters,
            int depth,
            string partitionKeyPrefix,
            string partitionKeyLowerBound,
            string partitionKeyUpperBound)
            : base(parameters, depth)
        {
            PartitionKeyPrefix = partitionKeyPrefix ?? throw new ArgumentNullException(nameof(partitionKeyPrefix));
            PartitionKeyLowerBound = partitionKeyLowerBound ?? throw new ArgumentNullException(nameof(partitionKeyLowerBound));
            PartitionKeyUpperBound = partitionKeyUpperBound ?? throw new ArgumentNullException(nameof(partitionKeyLowerBound));
        }

        public override string DebuggerDisplay => $"prefix query PK = '{PartitionKeyPrefix}*', PK > '{PartitionKeyLowerBound}', PK < '{PartitionKeyUpperBound}'";

        public string PartitionKeyPrefix { get; }
        public string PartitionKeyLowerBound { get; }
        public string PartitionKeyUpperBound { get; }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;

namespace NuGet.Insights.TablePrefixScan
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public class TablePrefixScanStart : TablePrefixScanStep
    {
        public TablePrefixScanStart(
            TableQueryParameters parameters,
            string partitionKeyPrefix,
            string partitionKeyLowerBound,
            string partitionKeyUpperBound)
            : base(parameters, depth: 0)
        {
            PartitionKeyPrefix = partitionKeyPrefix ?? throw new ArgumentNullException(nameof(partitionKeyPrefix));
            PartitionKeyLowerBound = partitionKeyLowerBound;
            PartitionKeyUpperBound = partitionKeyUpperBound;
        }

        public override string DebuggerDisplay => $"start PK = '{PartitionKeyPrefix}*', PK > '{PartitionKeyLowerBound}', PK < '{PartitionKeyUpperBound}'";

        public string PartitionKeyPrefix { get; }
        public string PartitionKeyLowerBound { get; }
        public string PartitionKeyUpperBound { get; }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Azure.Data.Tables;

namespace NuGet.Insights.TablePrefixScan
{
    public class TableQueryParameters
    {
        public TableQueryParameters(TableClient table, IList<string> selectColumns, int takeCount, bool expandPartitionKeys)
        {
            Table = table ?? throw new ArgumentNullException(nameof(table));
            SelectColumns = selectColumns;
            TakeCount = takeCount;
            ExpandPartitionKeys = expandPartitionKeys;
        }

        public TableClient Table { get; }
        public IList<string> SelectColumns { get; }
        public int TakeCount { get; }
        public bool ExpandPartitionKeys { get; }
    }
}

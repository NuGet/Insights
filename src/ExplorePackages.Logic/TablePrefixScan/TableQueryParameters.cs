using System;
using System.Collections.Generic;
using Azure.Data.Tables;

namespace Knapcode.ExplorePackages.TablePrefixScan
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

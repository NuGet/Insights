using System;
using System.Collections.Generic;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.TablePrefixScan
{
    public class TableQueryParameters
    {
        public TableQueryParameters(CloudTable table, IList<string> selectColumns, int takeCount, bool expandPartitionKeys)
        {
            Table = table ?? throw new ArgumentNullException(nameof(table));
            SelectColumns = selectColumns;
            TakeCount = takeCount;
            ExpandPartitionKeys = expandPartitionKeys;
        }

        public CloudTable Table { get; }
        public IList<string> SelectColumns { get; }
        public int TakeCount { get; }
        public bool ExpandPartitionKeys { get; }
    }
}

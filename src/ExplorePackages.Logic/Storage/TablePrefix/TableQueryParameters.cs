using System;
using System.Collections.Generic;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages
{
    public class TableQueryParameters
    {
        public TableQueryParameters(CloudTable table, IList<string> selectColumns, int takeCount)
        {
            Table = table ?? throw new ArgumentNullException(nameof(table));
            SelectColumns = selectColumns;
            TakeCount = takeCount;
        }

        public CloudTable Table { get; }
        public IList<string> SelectColumns { get; }
        public int TakeCount { get; }
    }
}

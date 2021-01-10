using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.TablePrefixScan
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public class TablePrefixScanEntitySegment<T> : TablePrefixScanStep where T : ITableEntity, new()
    {
        public TablePrefixScanEntitySegment(TableQueryParameters parameters, int depth, List<T> entities)
            : base(parameters, depth)
        {
            Entities = entities ?? throw new ArgumentNullException(nameof(Entities));

            if (entities.Count == 0)
            {
                throw new ArgumentException("There must be at least one entity.");
            }
        }

        public override string DebuggerDisplay
        {
            get
            {
                var first = Entities.First();
                if (Entities.Count > 1)
                {
                    var last = Entities.Last();
                    return $"entity segment (PK '{first.PartitionKey}', RK '{first.RowKey}') ... (PK '{last.PartitionKey}', RK '{last.RowKey}') ({Entities.Count} total)";
                }
                else
                {
                    return $"entity segment (PK '{first.PartitionKey}', RK '{first.RowKey}') ({Entities.Count} total)";
                }
            }
        }

        public List<T> Entities { get; }
    }
}

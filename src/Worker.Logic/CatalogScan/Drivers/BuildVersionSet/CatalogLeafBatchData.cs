using System;
using System.Collections.Generic;
using MessagePack;

namespace NuGet.Insights.Worker.BuildVersionSet
{
    [MessagePackObject]
    public record CatalogLeafBatchData
    {
        public CatalogLeafBatchData(DateTimeOffset maxCommitTimestamp, List<CatalogLeafItemData> leaves)
        {
            MaxCommitTimestamp = maxCommitTimestamp;
            Leaves = leaves;
        }

        [Key(0)]
        public DateTimeOffset MaxCommitTimestamp { get; }

        [Key(1)]
        public List<CatalogLeafItemData> Leaves { get; }
    }
}

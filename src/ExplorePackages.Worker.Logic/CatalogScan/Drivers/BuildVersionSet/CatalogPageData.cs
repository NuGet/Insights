using System;
using System.Collections.Generic;
using MessagePack;

namespace Knapcode.ExplorePackages.Worker.BuildVersionSet
{
    [MessagePackObject]
    public record CatalogPageData
    {
        public CatalogPageData(DateTimeOffset commitTimestamp, List<CatalogLeafItemData> leaves)
        {
            CommitTimestamp = commitTimestamp;
            Leaves = leaves;
        }

        [Key(0)]
        public DateTimeOffset CommitTimestamp { get; }

        [Key(1)]
        public List<CatalogLeafItemData> Leaves { get; }
    }
}

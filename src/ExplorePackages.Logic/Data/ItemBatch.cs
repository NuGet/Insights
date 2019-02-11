using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Logic
{
    public class ItemBatch<TItem, TProgressToken>
    {
        public ItemBatch(IReadOnlyList<TItem> items) : this(items, hasMoreItems: false, nextProgressToken: default(TProgressToken))
        {
        }

        public ItemBatch(IReadOnlyList<TItem> items, bool hasMoreItems, TProgressToken nextProgressToken)
        {
            Items = items;
            HasMoreItems = hasMoreItems;
            NextProgressToken = nextProgressToken;
        }

        public IReadOnlyList<TItem> Items { get; }
        public bool HasMoreItems { get; }
        public TProgressToken NextProgressToken { get; }
    }
}

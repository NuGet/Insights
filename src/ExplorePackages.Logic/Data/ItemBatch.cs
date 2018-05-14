using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Logic
{
    public class ItemBatch<T>
    {
        public ItemBatch(IReadOnlyList<T> items, bool hasMoreItems)
        {
            Items = items;
            HasMoreItems = hasMoreItems;
        }

        public IReadOnlyList<T> Items { get; }
        public bool HasMoreItems { get; }
    }
}

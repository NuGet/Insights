// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Insights
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

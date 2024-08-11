// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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

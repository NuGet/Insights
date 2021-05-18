// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using MessagePack;

namespace NuGet.Insights.Worker.BuildVersionSet
{
    [MessagePackObject]
    public record CatalogLeafItemData
    {
        public CatalogLeafItemData(string lowerId, string lowerVersion, bool isDeleted)
        {
            LowerId = lowerId;
            LowerVersion = lowerVersion;
            IsDeleted = isDeleted;
        }

        [Key(0)]
        public string LowerId { get; }

        [Key(1)]
        public string LowerVersion { get; }

        [Key(2)]
        public bool IsDeleted { get; }
    }
}

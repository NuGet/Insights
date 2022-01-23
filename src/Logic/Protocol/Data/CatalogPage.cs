// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NuGet.Insights
{
    public class CatalogPage
    {
        [JsonPropertyName("commitTimeStamp")]
        public DateTimeOffset CommitTimestamp { get; set; }

        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("items")]
        public List<CatalogLeafItem> Items { get; set; }

        [JsonPropertyName("parent")]
        public string Parent { get; set; }
    }
}

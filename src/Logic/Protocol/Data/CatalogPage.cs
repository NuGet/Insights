// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class CatalogPage
    {
        [JsonPropertyName("@id")]
        public string Url { get; set; }

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

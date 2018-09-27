using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Logic
{
    public class CatalogPage
    {
        [JsonProperty("commitTimeStamp")]
        public DateTimeOffset CommitTimestamp { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }

        [JsonProperty("items")]
        public List<CatalogLeafItem> Items { get; set; }

        [JsonProperty("parent")]
        public string Parent { get; set; }
    }
}

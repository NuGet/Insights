using System;
using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class CatalogPageScanMessage
    {
        [JsonProperty("u")]
        public string Url { get; set; }

        [JsonProperty(">")]
        public DateTimeOffset Min { get; set; }

        [JsonProperty("<=")]
        public DateTimeOffset Max { get; set; }
    }
}

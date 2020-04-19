using System;
using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class CatalogIndexScanMessage
    {
        [JsonProperty(">")]
        public DateTimeOffset? Min { get; set; }

        [JsonProperty("<=")]
        public DateTimeOffset? Max { get; set; }
    }
}

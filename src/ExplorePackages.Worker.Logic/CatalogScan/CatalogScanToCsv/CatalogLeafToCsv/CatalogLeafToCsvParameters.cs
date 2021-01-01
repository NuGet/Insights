using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogLeafToCsvParameters
    {
        [JsonProperty("bc")]
        public int BucketCount { get; set; }

        [JsonProperty("ll")]
        public bool OnlyLatestLeaves { get; set; }
    }
}

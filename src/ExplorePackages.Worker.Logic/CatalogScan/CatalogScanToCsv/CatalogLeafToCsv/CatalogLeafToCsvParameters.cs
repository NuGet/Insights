using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogLeafToCsvParameters
    {
        [JsonProperty("bc")]
        public int BucketCount { get; set; }

        [JsonProperty("m")]
        public CatalogLeafToCsvMode Mode { get; set; }
    }
}

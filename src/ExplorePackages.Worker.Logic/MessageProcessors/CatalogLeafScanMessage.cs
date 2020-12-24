using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogLeafScanMessage
    {
        [JsonProperty("s")]
        public string StorageSuffix { get; set; }

        [JsonProperty("p0")]
        public string ScanId { get; set; }

        [JsonProperty("p1")]
        public string PageId { get; set; }

        [JsonProperty("r")]
        public string LeafId { get; set; }

        [JsonProperty("ac")]
        public int AttemptCount { get; set; }
    }
}

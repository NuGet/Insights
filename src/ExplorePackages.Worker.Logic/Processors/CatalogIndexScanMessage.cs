using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogIndexScanMessage
    {
        [JsonProperty("i")]
        public string ScanId { get; set; }

        [JsonProperty("ac")]
        public int AttemptCount { get; set; }
    }
}

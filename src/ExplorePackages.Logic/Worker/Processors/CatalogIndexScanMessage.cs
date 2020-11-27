using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogIndexScanMessage
    {
        [JsonProperty("i")]
        public string ScanId { get; set; }
    }
}

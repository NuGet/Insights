using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class CatalogPageScanMessage
    {
        [JsonProperty("p")]
        public string ScanId { get; set; }

        [JsonProperty("r")]
        public string PageId { get; set; }
    }
}

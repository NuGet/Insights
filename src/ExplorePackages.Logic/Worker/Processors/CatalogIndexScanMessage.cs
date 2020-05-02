using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class CatalogIndexScanMessage
    {
        [JsonProperty("i")]
        public string ScanId { get; set; }
    }
}

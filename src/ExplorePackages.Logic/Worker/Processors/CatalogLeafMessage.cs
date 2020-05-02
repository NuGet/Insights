using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class CatalogLeafMessage
    {
        [JsonProperty("p0")]
        public string ScanId { get; set; }

        [JsonProperty("p1")]
        public string PageId { get; set; }

        [JsonProperty("r")]
        public string LeafId { get; set; }
    }
}

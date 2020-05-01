using Knapcode.ExplorePackages.Entities;
using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class CatalogLeafMessage
    {
        [JsonProperty("t")]
        public CatalogScanType ScanType { get; set; }

        [JsonProperty("l")]
        public CatalogLeafType LeafType { get; set; }

        [JsonProperty("u")]
        public string Url { get; set; }
    }
}

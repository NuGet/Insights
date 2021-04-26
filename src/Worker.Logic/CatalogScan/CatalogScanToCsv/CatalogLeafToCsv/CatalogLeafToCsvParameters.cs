using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogLeafToCsvParameters
    {
        [JsonProperty("m")]
        public CatalogLeafToCsvMode Mode { get; set; }
    }
}

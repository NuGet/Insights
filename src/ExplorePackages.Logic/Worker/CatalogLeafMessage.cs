using Knapcode.ExplorePackages.Entities;
using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class CatalogLeafMessage
    {
        [JsonProperty("t")]
        public CatalogLeafType Type { get; set; }

        [JsonProperty("u")]
        public string Url { get; set; }
    }
}

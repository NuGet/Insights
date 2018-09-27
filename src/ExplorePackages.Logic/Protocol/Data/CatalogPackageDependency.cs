using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Logic
{
    public class CatalogPackageDependency
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("range")]
        [JsonConverter(typeof(CatalogPackageDependencyRangeConverter))]
        public string Range { get; set; }
    }
}

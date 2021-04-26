using Newtonsoft.Json;

namespace Knapcode.ExplorePackages
{
    public class RegistrationCatalogEntry
    {
        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("listed")]
        public bool Listed { get; set; }
    }
}

using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Logic
{
    public class RegistrationCatalogEntry
    {
        [JsonProperty("version")]
        public string Version { get; set; }
    }
}

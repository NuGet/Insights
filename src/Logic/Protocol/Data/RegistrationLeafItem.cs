using Newtonsoft.Json;

namespace Knapcode.ExplorePackages
{
    public class RegistrationLeafItem
    {
        [JsonProperty("catalogEntry")]
        public RegistrationCatalogEntry CatalogEntry { get; set; }
    }
}

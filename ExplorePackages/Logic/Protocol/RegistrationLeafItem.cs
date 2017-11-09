using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Logic
{
    public class RegistrationLeafItem
    {
        [JsonProperty("catalogEntry")]
        public RegistrationCatalogEntry CatalogEntry { get; set; }
    }
}

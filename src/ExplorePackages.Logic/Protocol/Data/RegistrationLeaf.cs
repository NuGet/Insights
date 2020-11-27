using Newtonsoft.Json;

namespace Knapcode.ExplorePackages
{
    public class RegistrationLeaf
    {
        [JsonProperty("listed")]
        public bool Listed { get; set; }
    }
}

using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Logic
{
    public class RegistrationLeaf
    {
        [JsonProperty("listed")]
        public bool Listed { get; set; }
    }
}

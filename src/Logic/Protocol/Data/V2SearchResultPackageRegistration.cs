using Newtonsoft.Json;

namespace Knapcode.ExplorePackages
{
    public class V2SearchResultPackageRegistration
    {
        [JsonProperty("Id")]
        public string Id { get; set; }
    }
}

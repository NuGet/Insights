using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Worker.FindLatestLeaves
{
    public class FindLatestLeavesParameters
    {
        [JsonProperty("p")]
        public string Prefix { get; set; }

        [JsonProperty("t")]
        public string StorageSuffix { get; set; }
    }
}

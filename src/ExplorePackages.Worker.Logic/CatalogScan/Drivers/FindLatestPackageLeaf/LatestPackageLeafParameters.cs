using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Worker.FindLatestPackageLeaf
{
    public class LatestPackageLeafParameters
    {
        [JsonProperty("p")]
        public string Prefix { get; set; }

        [JsonProperty("t")]
        public string StorageSuffix { get; set; }
    }
}

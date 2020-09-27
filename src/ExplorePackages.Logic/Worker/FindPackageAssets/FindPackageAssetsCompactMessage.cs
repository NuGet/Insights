using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Logic.Worker.FindPackageAssets
{
    public class FindPackageAssetsCompactMessage
    {
        [JsonProperty("p")]
        public string Prefix { get; set; }

        [JsonProperty("b")]
        public int Bucket { get; set; }
    }
}

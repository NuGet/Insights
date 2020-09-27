using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Logic.Worker.FindPackageAssets
{
    public class FindPackageAssetsCompactMessage
    {
        [JsonProperty("b")]
        public int Bucket { get; set; }
    }
}

using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Worker.FindPackageAssets
{
    public class FindPackageAssetsParameters
    {
        [JsonProperty("bc")]
        public int BucketCount { get; set; }
    }
}

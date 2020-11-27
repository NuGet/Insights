using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Worker
{
    public class FindLatestLeavesParameters
    {
        [JsonProperty("p")]
        public string Prefix { get; set; }
    }
}

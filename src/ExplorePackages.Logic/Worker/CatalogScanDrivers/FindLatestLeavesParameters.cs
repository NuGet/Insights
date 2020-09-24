using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class FindLatestLeavesParameters
    {
        [JsonProperty("p")]
        public string Prefix { get; set; }
    }
}

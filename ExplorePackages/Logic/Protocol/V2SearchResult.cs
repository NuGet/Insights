using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Logic
{
    public class V2SearchResult
    {
        [JsonProperty("totalHits")]
        public int TotalHits { get; set; }
    }
}

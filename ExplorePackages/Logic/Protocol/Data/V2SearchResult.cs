using System.Collections.Generic;
using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Logic
{
    public class V2SearchResult
    {
        [JsonProperty("totalHits")]
        public int TotalHits { get; set; }
        
        [JsonProperty("data")]
        public List<V2SearchResultItem> Data { get; set; }
    }
}

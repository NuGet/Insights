using System.Collections.Generic;
using Newtonsoft.Json;

namespace Knapcode.ExplorePackages
{
    public class AutocompleteVersionResults
    {
        [JsonProperty("data")]
        public List<string> Data { get; set; }
    }
}

using System.Collections.Generic;
using Newtonsoft.Json;

namespace Knapcode.ExplorePackages
{
    public class FlatContainerIndex
    {
        [JsonProperty("versions")]
        public List<string> Versions { get; set; }
    }
}

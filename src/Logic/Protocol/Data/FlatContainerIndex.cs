using System.Collections.Generic;
using Newtonsoft.Json;

namespace NuGet.Insights
{
    public class FlatContainerIndex
    {
        [JsonProperty("versions")]
        public List<string> Versions { get; set; }
    }
}

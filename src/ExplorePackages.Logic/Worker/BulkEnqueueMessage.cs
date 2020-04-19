using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class BulkEnqueueMessage
    {
        [JsonProperty("m")]
        public List<JToken> Messages { get; set; }
    }
}

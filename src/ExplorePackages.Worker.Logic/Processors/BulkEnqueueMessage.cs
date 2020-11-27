using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Worker
{
    public class BulkEnqueueMessage
    {
        [JsonProperty("m")]
        public List<JToken> Messages { get; set; }

        [JsonProperty("d")]
        public TimeSpan NotBefore { get; set; }
    }
}

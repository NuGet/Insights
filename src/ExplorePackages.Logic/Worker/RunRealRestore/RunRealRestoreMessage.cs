using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Worker.RunRealRestore
{
    public class RunRealRestoreMessage
    {
        [JsonProperty("i")]
        public string Id { get; set; }

        [JsonProperty("v")]
        public string Version { get; set; }

        [JsonProperty("f")]
        public string Framework { get; set; }
    }
}

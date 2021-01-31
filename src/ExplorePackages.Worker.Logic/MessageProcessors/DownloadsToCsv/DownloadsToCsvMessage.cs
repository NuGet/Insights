using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Worker.DownloadsToCsv
{
    public class DownloadsToCsvMessage : ITaskStateMessage
    {
        [JsonProperty("ts")]
        public TaskStateKey TaskStateKey { get; set; }

        [JsonProperty("ac")]
        public int AttemptCount { get; set; }

        [JsonProperty("l")]
        public bool Loop { get; set; }
    }
}

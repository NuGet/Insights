using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Worker.StreamWriterUpdater
{
    public class StreamWriterUpdaterMessage<T> : ILoopingMessage
    {
        [JsonProperty("ts")]
        public TaskStateKey TaskStateKey { get; set; }

        [JsonProperty("ac")]
        public int AttemptCount { get; set; }

        [JsonProperty("l")]
        public bool Loop { get; set; }
    }
}

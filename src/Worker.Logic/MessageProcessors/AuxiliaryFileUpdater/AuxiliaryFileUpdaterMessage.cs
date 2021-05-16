using Newtonsoft.Json;

namespace NuGet.Insights.Worker.AuxiliaryFileUpdater
{
    public class AuxiliaryFileUpdaterMessage<T> : ITaskStateMessage
    {
        [JsonProperty("ts")]
        public TaskStateKey TaskStateKey { get; set; }

        [JsonProperty("ac")]
        public int AttemptCount { get; set; }
    }
}

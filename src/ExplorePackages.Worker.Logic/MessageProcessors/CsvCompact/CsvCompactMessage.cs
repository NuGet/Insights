using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Worker
{
    public class CsvCompactMessage<T> where T : ICsvRecord<T>, new()
    {
        [JsonProperty("s")]
        public string SourceContainer { get; set; }

        [JsonProperty("b")]
        public int Bucket { get; set; }

        [JsonProperty("ts")]
        public TaskStateKey TaskStateKey { get; set; }

        [JsonProperty("f")]
        public bool Force { get; set; }
    }
}

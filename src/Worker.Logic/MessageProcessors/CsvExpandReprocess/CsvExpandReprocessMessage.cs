using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Worker
{
    public class CsvExpandReprocessMessage<T> where T : ICsvRecord
    {
        [JsonProperty("b")]
        public int Bucket { get; set; }

        [JsonProperty("ts")]
        public TaskStateKey TaskStateKey { get; set; }

        [JsonProperty("c")]
        public string CursorName { get; set; }

        [JsonProperty("i")]
        public string ScanId { get; set; }
    }
}

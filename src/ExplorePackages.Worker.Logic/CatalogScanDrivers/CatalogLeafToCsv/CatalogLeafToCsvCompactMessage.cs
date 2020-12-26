using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogLeafToCsvCompactMessage<T> where T : ICsvRecord<T>
    {
        [JsonProperty("s")]
        public string SourceContainer { get; set; }

        [JsonProperty("b")]
        public int Bucket { get; set; }

        [JsonProperty("ts")]
        public string TaskStateStorageSuffix { get; set; }

        [JsonProperty("tp")]
        public string TaskStatePartitionKey { get; set; }

        [JsonProperty("tr")]
        public string TaskStateRowKey { get; set; }

        [JsonProperty("f")]
        public bool Force { get; set; }
    }
}

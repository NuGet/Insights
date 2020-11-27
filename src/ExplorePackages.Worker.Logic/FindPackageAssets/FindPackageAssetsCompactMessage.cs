using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Worker.FindPackageAssets
{
    public class FindPackageAssetsCompactMessage
    {
        [JsonProperty("s")]
        public string SourceContainer { get; set; }

        [JsonProperty("d")]
        public string DestinationContainer { get; set; }

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

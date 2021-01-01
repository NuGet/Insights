using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Worker
{
    public class TablePrefixScanPrefixQueryParameters
    {
        [JsonProperty("d")]
        public int Depth { get; set; }

        [JsonProperty("p")]
        public string PartitionKeyPrefix { get; set; }

        [JsonProperty("m")]
        public string PartitionKeyLowerBound { get; set; }
    }
}

using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Worker
{
    public class TablePrefixScanPrefixQueryParameters
    {
        [JsonProperty("sf")]
        public int SegmentsPerFirstPrefix { get; set; }

        [JsonProperty("ss")]
        public int SegmentsPerSubsequentPrefix { get; set; }

        [JsonProperty("d")]
        public int Depth { get; set; }

        [JsonProperty("p")]
        public string PartitionKeyPrefix { get; set; }

        [JsonProperty("m")]
        public string PartitionKeyLowerBound { get; set; }
    }
}

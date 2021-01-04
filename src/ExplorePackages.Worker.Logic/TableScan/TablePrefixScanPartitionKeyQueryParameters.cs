using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Worker
{
    public class TablePrefixScanPartitionKeyQueryParameters
    {
        [JsonProperty("sf")]
        public int SegmentsPerFirstPrefix { get; set; }

        [JsonProperty("ss")]
        public int SegmentsPerSubsequentPrefix { get; set; }

        [JsonProperty("d")]
        public int Depth { get; set; }

        [JsonProperty("p")]
        public string PartitionKey { get; set; }

        [JsonProperty("r")]
        public string RowKeySkip { get; set; }
    }
}

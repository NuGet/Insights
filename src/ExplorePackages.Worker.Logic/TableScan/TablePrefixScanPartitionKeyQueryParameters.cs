using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Worker
{
    public class TablePrefixScanPartitionKeyQueryParameters
    {
        [JsonProperty("d")]
        public int Depth { get; set; }

        [JsonProperty("p")]
        public string PartitionKey { get; set; }

        [JsonProperty("r")]
        public string RowKeySkip { get; set; }
    }
}

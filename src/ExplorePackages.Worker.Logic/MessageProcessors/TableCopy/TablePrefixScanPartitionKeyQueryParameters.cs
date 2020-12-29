using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Worker.TableCopy
{
    public class TablePrefixScanPartitionKeyQueryParameters : TablePrefixScanStepParameters
    {
        [JsonProperty("d")]
        public int Depth { get; set; }

        [JsonProperty("p")]
        public string PartitionKey { get; set; }

        [JsonProperty("r")]
        public string RowKeySkip { get; set; }
    }
}

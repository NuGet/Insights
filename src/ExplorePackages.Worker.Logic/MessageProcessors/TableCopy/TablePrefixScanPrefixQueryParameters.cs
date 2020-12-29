using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Worker.TableCopy
{
    public class TablePrefixScanPrefixQueryParameters : TablePrefixScanStepParameters
    {
        [JsonProperty("d")]
        public int Depth { get; set; }

        [JsonProperty("p")]
        public string PartitionKeyPrefix { get; set; }

        [JsonProperty("m")]
        public string PartitionKeyLowerBound { get; set; }
    }
}

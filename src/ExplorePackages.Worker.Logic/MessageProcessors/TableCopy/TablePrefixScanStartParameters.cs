using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Worker.TableCopy
{
    public class TablePrefixScanStartParameters : TablePrefixScanStepParameters
    {
        [JsonProperty("p")]
        public string PartitionKeyPrefix { get; set; }
    }
}

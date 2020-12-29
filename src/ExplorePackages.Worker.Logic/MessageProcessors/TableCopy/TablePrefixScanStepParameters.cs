using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Worker.TableCopy
{
    public abstract class TablePrefixScanStepParameters
    {
        [JsonProperty("t")]
        public int TakeCount { get; set; }
    }
}

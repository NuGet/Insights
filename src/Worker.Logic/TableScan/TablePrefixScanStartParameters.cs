using Newtonsoft.Json;

namespace NuGet.Insights.Worker
{
    public class TablePrefixScanStartParameters
    {
        [JsonProperty("sf")]
        public int SegmentsPerFirstPrefix { get; set; }

        [JsonProperty("ss")]
        public int SegmentsPerSubsequentPrefix { get; set; }
    }
}

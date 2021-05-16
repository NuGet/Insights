using Newtonsoft.Json;

namespace NuGet.Insights.Worker
{
    public class CatalogIndexScanMessage
    {
        [JsonProperty("c")]
        public string CursorName { get; set; }

        [JsonProperty("i")]
        public string ScanId { get; set; }

        [JsonProperty("ac")]
        public int AttemptCount { get; set; }
    }
}

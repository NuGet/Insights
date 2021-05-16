using Newtonsoft.Json;

namespace NuGet.Insights
{
    public class SearchDiagnostics
    {
        [JsonProperty("SearchIndex")]
        public SearchIndexDiagnosticsData SearchIndex { get; set; }

        [JsonProperty("HijackIndex")]
        public SearchIndexDiagnosticsData HijackIndex { get; set; }
    }
}

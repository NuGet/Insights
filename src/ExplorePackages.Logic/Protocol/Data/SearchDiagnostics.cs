using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Logic
{
    public class SearchDiagnostics
    {
        [JsonProperty("SearchIndex")]
        public SearchIndexDiagnosticsData SearchIndex { get; set; }

        [JsonProperty("HijackIndex")]
        public SearchIndexDiagnosticsData HijackIndex { get; set; }
    }
}

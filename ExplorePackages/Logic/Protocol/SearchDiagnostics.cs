using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Logic
{
    public class SearchDiagnostics
    {
        [JsonProperty("CommitUserData")]
        public SearchDiagnosticsCommitUserData CommitUserData { get; set; }
    }
}

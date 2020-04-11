using System;
using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Logic
{
    public class SearchIndexDiagnosticsData
    {
        [JsonProperty("LastCommitTimestamp")]
        public DateTimeOffset LastCommitTimestamp { get; set; }
    }
}

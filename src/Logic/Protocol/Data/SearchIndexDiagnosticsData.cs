using System;
using Newtonsoft.Json;

namespace Knapcode.ExplorePackages
{
    public class SearchIndexDiagnosticsData
    {
        [JsonProperty("LastCommitTimestamp")]
        public DateTimeOffset LastCommitTimestamp { get; set; }
    }
}

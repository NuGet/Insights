using System;
using Newtonsoft.Json;

namespace NuGet.Insights
{
    public class SearchIndexDiagnosticsData
    {
        [JsonProperty("LastCommitTimestamp")]
        public DateTimeOffset LastCommitTimestamp { get; set; }
    }
}

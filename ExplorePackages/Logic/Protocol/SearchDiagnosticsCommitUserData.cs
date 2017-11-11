using System;
using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Logic
{
    public class SearchDiagnosticsCommitUserData
    {
        [JsonProperty("commitTimeStamp")]
        public DateTimeOffset CommitTimestamp { get; set; }
    }
}

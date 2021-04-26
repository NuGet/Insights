using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Worker.TableCopy
{
    public class TableCopyParameters
    {
        [JsonProperty("d")]
        public string DestinationTableName { get; set; }
    }
}

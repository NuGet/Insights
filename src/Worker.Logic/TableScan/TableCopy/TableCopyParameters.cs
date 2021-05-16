using Newtonsoft.Json;

namespace NuGet.Insights.Worker.TableCopy
{
    public class TableCopyParameters
    {
        [JsonProperty("d")]
        public string DestinationTableName { get; set; }
    }
}

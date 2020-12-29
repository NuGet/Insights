using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Worker.TableCopy
{
    public class TableCopyMessage<T> where T : ITableEntity, new()
    {
        [JsonProperty("t")]
        public TableCopyStrategy Strategy { get; set; }

        [JsonProperty("s")]
        public string SourceTableName { get; set; }

        [JsonProperty("d")]
        public string DestinationTableName { get; set; }

        [JsonProperty("p")]
        public JToken Parameters { get; set; }
    }
}

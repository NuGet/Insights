using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Worker.TableCopy
{
    public class TableRowCopyMessage<T> where T : ITableEntity, new()
    {
        [JsonProperty("s")]
        public string SourceTableName { get; set; }

        [JsonProperty("d")]
        public string DestinationTableName { get; set; }

        [JsonProperty("p")]
        public string PartitionKey { get; set; }

        [JsonProperty("r")]
        public List<string> RowKeys { get; set; }
    }
}

using System;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Worker
{
    public class TableScanMessage<T> where T : ITableEntity, new()
    {
        [JsonProperty("b")]
        public DateTimeOffset Started { get; set; }

        [JsonProperty("ts")]
        public TaskStateKey TaskStateKey { get; set; }

        [JsonProperty("t")]
        public TableScanDriverType DriverType { get; set; }

        [JsonProperty("n")]
        public string TableName { get; set; }

        [JsonProperty("s")]
        public TableScanStrategy Strategy { get; set; }

        [JsonProperty("c")]
        public int TakeCount { get; set; }

        [JsonProperty("p")]
        public string PartitionKeyPrefix { get; set; }

        [JsonProperty("v")]
        public JToken ScanParameters { get; set; }

        [JsonProperty("d")]
        public JToken DriverParameters { get; set; }
    }
}

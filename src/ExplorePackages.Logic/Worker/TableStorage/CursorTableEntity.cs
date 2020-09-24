using System;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class CursorTableEntity : TableEntity
    {
        private static readonly DateTimeOffset Min = new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public CursorTableEntity(string name)
        {
            PartitionKey = string.Empty;
            RowKey = name;
        }

        public CursorTableEntity()
        {
        }

        [JsonIgnore]
        public string Name => RowKey;

        public DateTimeOffset Value { get; set; } = Min;
    }
}

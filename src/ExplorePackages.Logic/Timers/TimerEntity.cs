using System;
using Azure;
using Azure.Data.Tables;

namespace Knapcode.ExplorePackages
{
    public class TimerEntity : ITableEntity
    {
        public TimerEntity()
        {
        }

        public TimerEntity(string name)
        {
            PartitionKey = string.Empty;
            RowKey = name;
            Name = name;
        }

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public string Name { get; set; }
        public DateTimeOffset? LastExecuted { get; set; }
        public bool IsEnabled { get; set; }
    }
}

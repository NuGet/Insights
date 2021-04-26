using System;
using Azure;
using Azure.Data.Tables;

namespace Knapcode.ExplorePackages.Worker
{
    public class TaskState : ITableEntity
    {
        public TaskState()
        {
        }

        public TaskState(string storageSuffix, string partitionKey, string rowKey)
        {
            StorageSuffix = storageSuffix;
            PartitionKey = partitionKey;
            RowKey = rowKey;
        }

        public TaskStateKey GetKey()
        {
            return new TaskStateKey(StorageSuffix, PartitionKey, RowKey);
        }

        public string StorageSuffix { get; set; }
        public string Parameters { get; set; }

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}

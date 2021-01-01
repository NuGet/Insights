using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.Worker
{
    public class TaskState : TableEntity
    {
        public TaskState(string storageSuffix, string partitionKey, string rowKey) : this()
        {
            StorageSuffix = storageSuffix;
            PartitionKey = partitionKey;
            RowKey = rowKey;
        }

        public TaskState()
        {
        }

        [IgnoreProperty]
        public TaskStateKey Key => new TaskStateKey(StorageSuffix, PartitionKey, RowKey);

        public string StorageSuffix { get; set; }

        public string Parameters { get; set; }
    }
}

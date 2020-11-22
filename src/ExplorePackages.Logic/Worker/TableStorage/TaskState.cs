using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.Logic.Worker
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

        public string StorageSuffix { get; set; }
    }
}

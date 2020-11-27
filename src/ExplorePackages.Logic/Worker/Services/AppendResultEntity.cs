using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.Worker
{
    public class AppendResultEntity : TableEntity
    {
        public AppendResultEntity()
        {
        }

        public AppendResultEntity(int bucket, string id)
        {
            PartitionKey = bucket.ToString();
            RowKey = id;
        }

        public string Data { get; set; }
    }
}

using System;
using Azure;
using Azure.Data.Tables;

namespace Knapcode.ExplorePackages.Worker.KustoIngestion
{
    public class KustoBlobIngestion : ITableEntity
    {
        public KustoBlobIngestion()
        {
        }

        public KustoBlobIngestion(string containerName, int bucket)
        {
            PartitionKey = containerName;
            RowKey = bucket.ToString();
            Bucket = bucket;
        }

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public string IngestionId { get; set; }
        public string StorageSuffix { get; set; }
        public KustoBlobIngestionState State { get; set; }
        public int Bucket { get; set; }
        public long RawSizeBytes { get; set; }
        public string SourceUrl { get; set; }
        public string StatusUrl { get; set; }
        public Guid SourceId { get; set; }

        public string GetContainerName()
        {
            return PartitionKey;
        }
    }
}

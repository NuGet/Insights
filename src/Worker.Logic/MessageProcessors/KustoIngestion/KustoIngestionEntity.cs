using System;
using Azure;
using Azure.Data.Tables;

namespace NuGet.Insights.Worker.KustoIngestion
{
    public class KustoIngestionEntity : ITableEntity
    {
        public static readonly string DefaultPartitionKey = string.Empty;

        public KustoIngestionEntity()
        {
        }

        public KustoIngestionEntity(string ingestionId, string storageSuffix)
        {
            PartitionKey = DefaultPartitionKey;
            RowKey = ingestionId;
            StorageSuffix = storageSuffix;
            Created = DateTimeOffset.UtcNow;
            State = KustoIngestionState.Created;
        }

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public string StorageSuffix { get; set; }
        public KustoIngestionState State { get; set; }
        public DateTimeOffset Created { get; set; }
        public DateTimeOffset? Started { get; set; }
        public DateTimeOffset? Completed { get; set; }

        public string GetIngestionId()
        {
            return RowKey;
        }
    }
}

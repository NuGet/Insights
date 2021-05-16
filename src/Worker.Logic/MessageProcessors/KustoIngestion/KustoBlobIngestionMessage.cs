namespace NuGet.Insights.Worker.KustoIngestion
{
    public class KustoBlobIngestionMessage
    {
        public string StorageSuffix { get; set; }
        public string ContainerName { get; set; }
        public string BlobName { get; set; }
        public int AttemptCount { get; set; }
    }
}

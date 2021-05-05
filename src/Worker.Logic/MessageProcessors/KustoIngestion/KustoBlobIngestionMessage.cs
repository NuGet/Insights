namespace Knapcode.ExplorePackages.Worker.KustoIngestion
{
    public class KustoBlobIngestionMessage
    {
        public string StorageSuffix { get; set; }
        public string ContainerName { get; set; }
        public int Bucket { get; set; }
        public int AttemptCount { get; set; }
    }
}

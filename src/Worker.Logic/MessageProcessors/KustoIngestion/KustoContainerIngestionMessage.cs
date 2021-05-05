namespace Knapcode.ExplorePackages.Worker.KustoIngestion
{
    public class KustoContainerIngestionMessage
    {
        public string StorageSuffix { get; set; }
        public string ContainerName { get; set; }
        public int AttemptCount { get; set; }
    }
}

namespace Knapcode.ExplorePackages.Worker.KustoIngestion
{
    public class KustoIngestionMessage
    {
        public string IngestionId { get; set; }
        public int AttemptCount { get; set; }
    }
}

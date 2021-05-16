namespace NuGet.Insights.Worker.KustoIngestion
{
    public class KustoIngestionMessage
    {
        public string IngestionId { get; set; }
        public int AttemptCount { get; set; }
    }
}

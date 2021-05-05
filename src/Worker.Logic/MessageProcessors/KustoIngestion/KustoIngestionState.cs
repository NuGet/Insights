namespace Knapcode.ExplorePackages.Worker.KustoIngestion
{
    public enum KustoIngestionState
    {
        Created,
        Expanding,
        Enqueuing,
        Working,
        Finalizing,
        Complete,
    }
}

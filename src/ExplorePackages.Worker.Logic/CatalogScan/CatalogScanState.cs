namespace Knapcode.ExplorePackages.Worker
{
    public enum CatalogScanState
    {
        Created,
        Expanding,
        Enqueuing,
        Waiting,
        StartingAggregate,
        Aggregating,
        Finalizing,
        Complete,
    }
}

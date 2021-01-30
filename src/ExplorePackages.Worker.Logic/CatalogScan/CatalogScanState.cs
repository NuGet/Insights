namespace Knapcode.ExplorePackages.Worker
{
    public enum CatalogScanState
    {
        Created,
        WaitingOnDependency,
        Expanding,
        Enqueuing,
        Working,
        StartingAggregate,
        Aggregating,
        Finalizing,
        Complete,
    }
}

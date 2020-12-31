namespace Knapcode.ExplorePackages.Worker
{
    public enum CatalogScanState
    {
        Created,
        WaitingOnDependency,
        Expanding,
        Enqueuing,
        Waiting,
        StartingAggregate,
        Aggregating,
        Finalizing,
        Complete,
    }
}

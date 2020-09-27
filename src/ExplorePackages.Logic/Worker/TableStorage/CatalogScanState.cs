namespace Knapcode.ExplorePackages.Logic.Worker
{
    public enum CatalogScanState
    {
        Created,
        Expanding,
        Enqueuing,
        Waiting,
        Aggregating,
        Complete,
    }
}

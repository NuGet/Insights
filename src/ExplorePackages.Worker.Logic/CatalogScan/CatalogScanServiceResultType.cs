namespace Knapcode.ExplorePackages.Worker
{
    public enum CatalogScanServiceResultType
    {
        AlreadyRunning,
        UnavailableLease,
        NewStarted,
        BlockedByDependency,
        MinAfterMax,
        FullyCaughtUpWithDependency,
        FullyCaughtUpWithMax,
        Disabled,
    }
}

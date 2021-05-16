namespace NuGet.Insights.Worker
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

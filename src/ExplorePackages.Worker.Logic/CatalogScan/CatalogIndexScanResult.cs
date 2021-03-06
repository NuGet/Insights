namespace Knapcode.ExplorePackages.Worker
{
    public enum CatalogIndexScanResult
    {
        Processed,
        ExpandAllLeaves,
        ExpandLatestLeaves,
        ExpandLatestLeavesPerId,
        ExpandCustom,
    }
}

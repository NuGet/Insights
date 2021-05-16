namespace NuGet.Insights.Worker
{
    public enum CatalogIndexScanResult
    {
        Processed,
        ExpandAllLeaves,
        ExpandLatestLeaves,
        ExpandLatestLeavesPerId,
        CustomExpand,
    }
}

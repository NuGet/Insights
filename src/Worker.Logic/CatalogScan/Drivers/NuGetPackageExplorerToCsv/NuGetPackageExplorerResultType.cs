namespace NuGet.Insights.Worker.NuGetPackageExplorerToCsv
{
    public enum NuGetPackageExplorerResultType
    {
        Deleted,
        Available,
        Timeout,
        InvalidMetadata,
        NothingToValidate,
    }
}

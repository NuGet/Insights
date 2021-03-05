namespace Knapcode.ExplorePackages.Worker.NuGetPackageExplorerToCsv
{
    public enum NuGetPackageExplorerResultType
    {
        Deleted,
        Available,
        NoFiles,
        Timeout,
        Failed,
        InvalidMetadata,
    }
}

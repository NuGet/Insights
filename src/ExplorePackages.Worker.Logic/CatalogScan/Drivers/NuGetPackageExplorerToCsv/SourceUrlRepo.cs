namespace Knapcode.ExplorePackages.Worker.NuGetPackageExplorerToCsv
{
    public abstract record SourceUrlRepo
    {
        public abstract string Type { get; }
    }
}

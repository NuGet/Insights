namespace NuGet.Insights.Worker.NuGetPackageExplorerToCsv
{
    public abstract record SourceUrlRepo
    {
        public abstract string Type { get; }
    }
}

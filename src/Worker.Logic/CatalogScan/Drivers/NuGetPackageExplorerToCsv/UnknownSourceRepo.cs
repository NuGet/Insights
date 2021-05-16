namespace NuGet.Insights.Worker.NuGetPackageExplorerToCsv
{
    public record UnknownSourceRepo : SourceUrlRepo
    {
        public override string Type => "Unknown";
        public string Host { get; init; }
    }
}

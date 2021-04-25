namespace Knapcode.ExplorePackages.Worker.NuGetPackageExplorerToCsv
{
    public record GitHubSourceRepo : SourceUrlRepo
    {
        public override string Type => "GitHub";
        public string Owner { get; init; }
        public string Repo { get; init; }
        public string Ref { get; init; }
    }
}

namespace Knapcode.ExplorePackages.Worker.NuGetPackageExplorerToCsv
{
    public record InvalidSourceRepo : SourceUrlRepo
    {
        public override string Type => "Invalid";
    }
}

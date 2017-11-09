namespace Knapcode.ExplorePackages.Logic
{
    public class PackageQueryContext
    {
        public PackageQueryContext(ImmutablePackage package, NuspecQueryContext nuspec, bool isSemVer2)
        {
            Package = package;
            Nuspec = nuspec;
            IsSemVer2 = isSemVer2;
        }

        public ImmutablePackage Package { get; }
        public NuspecQueryContext Nuspec { get; }
        public bool IsSemVer2 { get; }
    }
}

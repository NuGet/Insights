namespace Knapcode.ExplorePackages.Logic
{
    public class PackageQueryContext
    {
        public PackageQueryContext(ImmutablePackage package, NuspecContext nuspec, bool isSemVer2, string fullVersion, bool isListed)
        {
            Package = package;
            Nuspec = nuspec;
            IsSemVer2 = isSemVer2;
            FullVersion = fullVersion;
            IsListed = isListed;
        }

        public ImmutablePackage Package { get; }
        public NuspecContext Nuspec { get; }
        public bool IsSemVer2 { get; }
        public string FullVersion { get; }
        public bool IsListed { get;  }
    }
}

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageQueryContext
    {
        public PackageQueryContext(
            ImmutablePackage package,
            NuspecContext nuspec,
            MZipContext mzip,
            bool isSemVer2,
            bool isListed)
        {
            Package = package;
            Nuspec = nuspec;
            MZip = mzip;
            IsSemVer2 = isSemVer2;
            IsListed = isListed;
        }

        public ImmutablePackage Package { get; }
        public NuspecContext Nuspec { get; }
        public MZipContext MZip { get; }
        public bool IsSemVer2 { get; }
        public bool IsListed { get;  }
    }
}

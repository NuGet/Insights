using Knapcode.ExplorePackages.Entities;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageQueryContext : PackageConsistencyContext
    {
        public PackageQueryContext(
            PackageEntity package,
            NuspecContext nuspec,
            MZipContext mzip) : base(package)
        {
            Package = package;
            Nuspec = nuspec;
            MZip = mzip;
        }

        public PackageEntity Package { get; }
        public NuspecContext Nuspec { get; }
        public MZipContext MZip { get; }
    }
}

namespace Knapcode.ExplorePackages.Entities
{
    public class PackageQueryContext : PackageConsistencyContext
    {
        public PackageQueryContext(
            PackageEntity package,
            NuspecContext nuspec,
            MZipContext mzip) : this(
                package.ToConsistencyContext(),
                package,
                nuspec,
                mzip)
        {
        }

        private PackageQueryContext(
            PackageConsistencyContext consistencyContext,
            PackageEntity package,
            NuspecContext nuspec,
            MZipContext mzip) : base(
                consistencyContext.Id,
                consistencyContext.Version,
                consistencyContext.IsDeleted,
                consistencyContext.IsSemVer2,
                consistencyContext.IsListed,
                consistencyContext.HasIcon)
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

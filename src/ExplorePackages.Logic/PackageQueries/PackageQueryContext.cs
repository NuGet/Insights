namespace Knapcode.ExplorePackages.Logic
{
    public class PackageQueryContext
    {
        public PackageQueryContext(
            string id,
            string version,
            NuspecContext nuspec,
            MZipContext mzip,
            bool isDeleted,
            bool isSemVer2,
            bool isListed)
        {
            Id = id;
            Version = version;
            Nuspec = nuspec;
            MZip = mzip;
            IsDeleted = isDeleted;
            IsSemVer2 = isSemVer2;
            IsListed = isListed;
        }

        public string Id { get; }
        public string Version { get; }
        public NuspecContext Nuspec { get; }
        public MZipContext MZip { get; }
        public bool IsDeleted { get; }
        public bool IsSemVer2 { get; }
        public bool IsListed { get;  }
    }
}

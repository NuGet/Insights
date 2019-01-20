namespace Knapcode.ExplorePackages.Logic
{
    public class PackageConsistencyContext
    {
        public PackageConsistencyContext(
            ImmutablePackage package,
            bool isSemVer2,
            bool isListed)
        {
            Package = package;
            IsSemVer2 = isSemVer2;
            IsListed = isListed;
        }

        public ImmutablePackage Package { get; }
        public bool IsSemVer2 { get; }
        public bool IsListed { get; }
    }
}

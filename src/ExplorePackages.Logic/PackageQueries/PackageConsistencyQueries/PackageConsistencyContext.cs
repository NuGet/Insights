namespace Knapcode.ExplorePackages.Logic
{
    public class PackageConsistencyContext
    {
        public PackageConsistencyContext(
            string id,
            string version,
            bool isDeleted,
            bool isSemVer2,
            bool isListed)
        {
            Id = id;
            Version = version;
            IsDeleted = isDeleted;
            IsSemVer2 = isSemVer2;
            IsListed = isListed;
        }

        public string Id { get; }
        public string Version { get; }
        public bool IsDeleted { get; }
        public bool IsSemVer2 { get; }
        public bool IsListed { get; }
    }
}

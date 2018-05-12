namespace Knapcode.ExplorePackages.Logic
{
    public class GalleryPackageState
    {
        public GalleryPackageState(
            string packageId,
            string packageVersion,
            PackageDeletedStatus packageDeletedStatus,
            bool isSemVer2,
            bool isListed)
        {
            PackageId = packageId;
            PackageVersion = packageVersion;
            PackageDeletedStatus = packageDeletedStatus;
            IsSemVer2 = isSemVer2;
            IsListed = isListed;
        }

        public string PackageId { get; }
        public string PackageVersion { get; }
        public PackageDeletedStatus PackageDeletedStatus { get; }
        public bool IsSemVer2 { get; }
        public bool IsListed { get; }
    }
}

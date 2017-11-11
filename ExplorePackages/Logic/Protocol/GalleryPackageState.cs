namespace Knapcode.ExplorePackages.Logic
{
    public class GalleryPackageState
    {
        public GalleryPackageState(PackageDeletedStatus packageDeletedStatus, bool isSemVer2, bool isListed)
        {
            PackageDeletedStatus = packageDeletedStatus;
            IsSemVer2 = isSemVer2;
            IsListed = isListed;
        }

        public PackageDeletedStatus PackageDeletedStatus { get; }
        public bool IsSemVer2 { get; }
        public bool IsListed { get; }
    }
}

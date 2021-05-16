namespace NuGet.Insights
{
    public class GalleryPackageState
    {
        public GalleryPackageState(
            string packageId,
            string packageVersion,
            PackageDeletedStatus packageDeletedStatus,
            bool isListed,
            bool hasIcon)
        {
            PackageId = packageId;
            PackageVersion = packageVersion;
            PackageDeletedStatus = packageDeletedStatus;
            IsListed = isListed;
            HasIcon = hasIcon;
        }

        public string PackageId { get; }
        public string PackageVersion { get; }
        public PackageDeletedStatus PackageDeletedStatus { get; }
        public bool IsListed { get; }
        public bool HasIcon { get; }
    }
}

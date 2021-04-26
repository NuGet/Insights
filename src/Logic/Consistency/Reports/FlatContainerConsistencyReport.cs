namespace Knapcode.ExplorePackages
{
    public class FlatContainerConsistencyReport : IConsistencyReport
    {
        public FlatContainerConsistencyReport(
           bool isConsistent,
           BlobMetadata packageContentMetadata,
           bool hasPackageManifest,
           bool hasIcon,
           bool isInIndex)
        {
            IsConsistent = isConsistent;
            PackageContentMetadata = packageContentMetadata;
            HasPackageManifest = hasPackageManifest;
            HasPackageIcon = hasIcon;
            IsInIndex = isInIndex;
        }

        public bool IsConsistent { get; }
        public BlobMetadata PackageContentMetadata { get; }
        public bool HasPackageManifest { get; }
        public bool HasPackageIcon { get; }
        public bool IsInIndex { get; }
    }
}

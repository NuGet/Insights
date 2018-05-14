namespace Knapcode.ExplorePackages.Logic
{
    public class FlatContainerConsistencyReport : IConsistencyReport
    {
        public FlatContainerConsistencyReport(
           bool isConsistent,
           BlobMetadata packageContentMetadata,
           bool hasPackageManifest,
           bool isInIndex)
        {
            IsConsistent = isConsistent;
            PackageContentMetadata = packageContentMetadata;
            HasPackageManifest = hasPackageManifest;
            IsInIndex = isInIndex;
        }

        public bool IsConsistent { get; }
        public BlobMetadata PackageContentMetadata { get; }
        public bool HasPackageManifest { get; }
        public bool IsInIndex { get; }
    }
}

namespace Knapcode.ExplorePackages
{
    public class PackagesContainerConsistencyReport : IConsistencyReport
    {
        public PackagesContainerConsistencyReport(
            bool isConsistent,
            BlobMetadata packageContentMetadata)
        {
            IsConsistent = isConsistent;
            PackageContentMetadata = packageContentMetadata;
        }

        public bool IsConsistent { get; }
        public BlobMetadata PackageContentMetadata { get; }
    }
}

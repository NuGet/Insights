using Knapcode.ExplorePackages.Support;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageConsistencyState
    {
        public PackageConsistencyState()
        {
            PackagesContainer = new PackagesContainerState();
            FlatContainer = new FlatContainerState();
        }

        public PackagesContainerState PackagesContainer { get; set; }
        public FlatContainerState FlatContainer { get; set; }

        public class PackagesContainerState
        {
            public BlobMetadata PackageContentMetadata { get; set; }
        }

        public class FlatContainerState
        {
            public BlobMetadata PackageContentMetadata { get; set; }
        }
    }
}

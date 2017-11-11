namespace Knapcode.ExplorePackages.Logic
{
    public class GalleryConsistencyReport : IConsistencyReport
    {
        public GalleryConsistencyReport(bool isConsistent, PackageDeletedStatus packageDeletedStatus)
        {
            IsConsistent = isConsistent;
            PackageDeletedStatus = packageDeletedStatus;
        }

        public bool IsConsistent { get; }
        public PackageDeletedStatus PackageDeletedStatus { get; }
    }
}

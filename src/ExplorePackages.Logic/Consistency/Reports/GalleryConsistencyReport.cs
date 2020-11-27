namespace Knapcode.ExplorePackages
{
    public class GalleryConsistencyReport : IConsistencyReport
    {
        public GalleryConsistencyReport(bool isConsistent, GalleryPackageState packageState)
        {
            IsConsistent = isConsistent;
            PackageState = packageState;
        }

        public bool IsConsistent { get; }
        public GalleryPackageState PackageState { get; }
    }
}

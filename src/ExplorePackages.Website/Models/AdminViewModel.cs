namespace Knapcode.ExplorePackages.Website.Models
{
    public class AdminViewModel
    {
        public int ApproximateMessageCount { get; set; }
        public int AvailableMessageCountLowerBound { get; set; }
        public bool AvailableMessageCountIsExact { get; set; }
        public int PoisonApproximateMessageCount { get; set; }
        public int PoisonAvailableMessageCountLowerBound { get; set; }
        public bool PoisonAvailableMessageCountIsExact { get; set; }

        public CatalogScanViewModel FindPackageAssets { get; set; }
        public CatalogScanViewModel FindPackageAssemblies { get; set; }
        public CatalogScanViewModel FindLatestLeaves { get; set; }
    }
}

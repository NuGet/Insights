namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogScanServiceResult
    {
        public CatalogScanServiceResult(CatalogScanServiceResultType type, string dependencyName, CatalogIndexScan scan)
        {
            Type = type;
            DependencyName = dependencyName;
            Scan = scan;
        }

        public CatalogScanServiceResultType Type { get; }
        public string DependencyName { get; }
        public CatalogIndexScan Scan { get; }
    }
}

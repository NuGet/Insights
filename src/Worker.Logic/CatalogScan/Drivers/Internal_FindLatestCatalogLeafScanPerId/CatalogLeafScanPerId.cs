namespace NuGet.Insights.Worker.FindLatestCatalogLeafScanPerId
{
    public class CatalogLeafScanPerId : CatalogLeafScan
    {
        public CatalogLeafScanPerId()
        {
        }

        public CatalogLeafScanPerId(string storageSuffix, string scanId, string pageId, string leafId)
            : base(storageSuffix, scanId, pageId, leafId)
        {
        }
    }
}

namespace Knapcode.ExplorePackages.Worker
{
    public interface ICatalogScanDriverFactory
    {
        ICatalogScanDriver Create(CatalogScanDriverType driverType);
        ICatalogLeafScanBatchDriver CreateBatchDriverOrNull(CatalogScanDriverType driverType);
        ICatalogLeafScanDriver CreateNonBatchDriver(CatalogScanDriverType driverType);
    }
}
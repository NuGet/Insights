namespace NuGet.Insights.Worker
{
    public interface ICatalogScanDriverFactory
    {
        ICatalogScanDriver Create(CatalogScanDriverType driverType);
        ICatalogLeafScanBatchDriver CreateBatchDriverOrNull(CatalogScanDriverType driverType);
        ICatalogLeafScanNonBatchDriver CreateNonBatchDriver(CatalogScanDriverType driverType);
    }
}
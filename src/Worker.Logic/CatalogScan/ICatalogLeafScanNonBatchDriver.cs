using System.Threading.Tasks;

namespace NuGet.Insights.Worker
{
    public interface ICatalogLeafScanNonBatchDriver : ICatalogScanDriver
    {
        Task<DriverResult> ProcessLeafAsync(CatalogLeafScan leafScan);
    }
}

using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker
{
    public interface ICatalogLeafScanNonBatchDriver : ICatalogScanDriver
    {
        Task<DriverResult> ProcessLeafAsync(CatalogLeafScan leafScan);
    }
}

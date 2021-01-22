using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker
{
    public interface ICatalogLeafScanDriver : ICatalogScanDriver
    {
        Task<DriverResult> ProcessLeafAsync(CatalogLeafScan leafScan);
    }
}

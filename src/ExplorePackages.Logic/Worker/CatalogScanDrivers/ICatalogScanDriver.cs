using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public interface ICatalogScanDriver
    {
        Task<CatalogIndexScanResult> ProcessIndexAsync(CatalogIndexScan indexScan);
        Task<CatalogPageScanResult> ProcessPageAsync(CatalogPageScan pageScan);
        Task ProcessLeafAsync(CatalogLeafScan leafScan);
        Task StartAggregateAsync(CatalogIndexScan indexScan);
        Task<bool> IsAggregateCompleteAsync(CatalogIndexScan indexScan);
    }
}

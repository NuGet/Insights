using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker
{
    public interface ICatalogScanDriver
    {
        Task<CatalogIndexScanResult> ProcessIndexAsync(CatalogIndexScan indexScan);
        Task<CatalogPageScanResult> ProcessPageAsync(CatalogPageScan pageScan);
        Task StartAggregateAsync(CatalogIndexScan indexScan);
        Task<bool> IsAggregateCompleteAsync(CatalogIndexScan indexScan);
        Task FinalizeAsync(CatalogIndexScan indexScan);
    }
}

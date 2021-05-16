using System.Threading.Tasks;

namespace NuGet.Insights.Worker
{
    public interface ICatalogScanDriver
    {
        Task InitializeAsync(CatalogIndexScan indexScan);
        Task<CatalogIndexScanResult> ProcessIndexAsync(CatalogIndexScan indexScan);
        Task StartCustomExpandAsync(CatalogIndexScan indexScan);
        Task<bool> IsCustomExpandCompleteAsync(CatalogIndexScan indexScan);
        Task<CatalogPageScanResult> ProcessPageAsync(CatalogPageScan pageScan);
        Task StartAggregateAsync(CatalogIndexScan indexScan);
        Task<bool> IsAggregateCompleteAsync(CatalogIndexScan indexScan);
        Task FinalizeAsync(CatalogIndexScan indexScan);
    }
}

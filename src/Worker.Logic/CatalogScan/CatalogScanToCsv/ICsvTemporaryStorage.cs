using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker
{
    public interface ICsvTemporaryStorage
    {
        Task AppendAsync<T>(string storageSuffix, ICsvRecordSet<T> set) where T : class, ICsvRecord;
        Task FinalizeAsync(CatalogIndexScan indexScan);
        Task InitializeAsync(CatalogIndexScan indexScan);
        Task<bool> IsAggregateCompleteAsync(CatalogIndexScan indexScan);
        Task<bool> IsCustomExpandCompleteAsync(CatalogIndexScan indexScan);
        Task StartAggregateAsync(CatalogIndexScan indexScan);
        Task StartCustomExpandAsync(CatalogIndexScan indexScan);
    }
}

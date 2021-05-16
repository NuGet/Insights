using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Insights.Worker
{
    public interface ILatestPackageLeafStorageFactory<T> where T : ILatestPackageLeaf
    {
        Task InitializeAsync();

        Task<ILatestPackageLeafStorage<T>> CreateAsync(
            CatalogPageScan pageScan,
            IReadOnlyDictionary<CatalogLeafItem, int> leafItemToRank);
    }
}

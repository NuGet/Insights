using System.Collections.Generic;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker
{
    public interface ILatestPackageLeafStorageFactory<T> where T : ILatestPackageLeaf
    {
        Task InitializeAsync();

        Task<ILatestPackageLeafStorage<T>> CreateAsync(
            CatalogPageScan pageScan,
            IReadOnlyDictionary<CatalogLeafItem, int> leafItemToRank);
    }
}

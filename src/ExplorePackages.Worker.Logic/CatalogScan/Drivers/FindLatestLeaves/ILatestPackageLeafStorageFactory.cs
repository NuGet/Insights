using System.Collections.Generic;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker.FindLatestLeaves
{
    public interface ILatestPackageLeafStorageFactory<T> where T : ILatestPackageLeaf
    {
        Task InitializeAsync(CatalogIndexScan indexScan);

        Task<ILatestPackageLeafStorage<T>> CreateAsync(
            CatalogPageScan pageScan,
            IReadOnlyDictionary<CatalogLeafItem, int> leafItemToRank);
    }
}

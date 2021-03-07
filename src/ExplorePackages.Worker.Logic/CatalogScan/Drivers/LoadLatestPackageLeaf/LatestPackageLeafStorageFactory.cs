using System.Collections.Generic;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker.LoadLatestPackageLeaf
{
    public class LatestPackageLeafStorageFactory : ILatestPackageLeafStorageFactory<LatestPackageLeaf>
    {
        private readonly LatestPackageLeafService _service;

        public LatestPackageLeafStorageFactory(LatestPackageLeafService service)
        {
            _service = service;
        }

        public async Task InitializeAsync(CatalogIndexScan indexScan)
        {
            await _service.InitializeAsync();
        }

        public Task<ILatestPackageLeafStorage<LatestPackageLeaf>> CreateAsync(CatalogPageScan pageScan, IReadOnlyDictionary<CatalogLeafItem, int> leafItemToRank)
        {
            var storage = new LatestPackageLeafStorage(
                _service.GetTable(),
                leafItemToRank,
                pageScan.Rank,
                pageScan.Url);
            return Task.FromResult<ILatestPackageLeafStorage<LatestPackageLeaf>>(storage);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.Worker.LoadLatestPackageLeaf
{
    public class LatestPackageLeafStorageFactory : ILatestPackageLeafStorageFactory<LatestPackageLeaf>
    {
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;

        public LatestPackageLeafStorageFactory(
            ServiceClientFactory serviceClientFactory,
            IOptions<ExplorePackagesWorkerSettings> options)
        {
            _serviceClientFactory = serviceClientFactory;
            _options = options;
        }

        public async Task InitializeAsync(CatalogIndexScan indexScan)
        {
            await GetTable().CreateIfNotExistsAsync(retry: true);
        }

        public Task<ILatestPackageLeafStorage<LatestPackageLeaf>> CreateAsync(CatalogPageScan pageScan, IReadOnlyDictionary<CatalogLeafItem, int> leafItemToRank)
        {
            var storage = new LatestPackageLeafStorage(
                GetTable(),
                leafItemToRank,
                pageScan.Rank,
                pageScan.Url);
            return Task.FromResult<ILatestPackageLeafStorage<LatestPackageLeaf>>(storage);
        }

        private CloudTable GetTable()
        {
            return _serviceClientFactory
                .GetStorageAccount()
                .CreateCloudTableClient()
                .GetTableReference(_options.Value.LatestPackageLeafTableName);
        }
    }
}

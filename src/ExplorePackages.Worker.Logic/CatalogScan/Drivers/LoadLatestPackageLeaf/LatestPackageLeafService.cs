using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.Worker.LoadLatestPackageLeaf
{
    public class LatestPackageLeafService
    {
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;

        public LatestPackageLeafService(
            ServiceClientFactory serviceClientFactory,
            IOptions<ExplorePackagesWorkerSettings> options)
        {
            _serviceClientFactory = serviceClientFactory;
            _options = options;
        }

        public async Task InitializeAsync()
        {
            await GetTable().CreateIfNotExistsAsync(retry: true);
        }

        public async Task<LatestPackageLeaf> GetOrNullAsync(string id, string version)
        {
            return await GetTable().RetrieveAsync<LatestPackageLeaf>(
                LatestPackageLeaf.GetPartitionKey(id),
                LatestPackageLeaf.GetRowKey(version));
        }

        internal CloudTable GetTable()
        {
            return _serviceClientFactory
                .GetStorageAccount()
                .CreateCloudTableClient()
                .GetTableReference(_options.Value.LatestPackageLeafTableName);
        }
    }
}

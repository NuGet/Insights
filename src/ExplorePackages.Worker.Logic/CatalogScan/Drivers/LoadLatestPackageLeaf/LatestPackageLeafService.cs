using System.Threading.Tasks;
using Azure.Data.Tables;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Worker.LoadLatestPackageLeaf
{
    public class LatestPackageLeafService
    {
        private readonly NewServiceClientFactory _serviceClientFactory;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;

        public LatestPackageLeafService(
            NewServiceClientFactory serviceClientFactory,
            IOptions<ExplorePackagesWorkerSettings> options)
        {
            _serviceClientFactory = serviceClientFactory;
            _options = options;
        }

        public async Task InitializeAsync()
        {
            await (await GetTableAsync()).CreateIfNotExistsAsync(retry: true);
        }

        public async Task<LatestPackageLeaf> GetOrNullAsync(string id, string version)
        {
            return await (await GetTableAsync()).GetEntityOrNullAsync<LatestPackageLeaf>(
                LatestPackageLeaf.GetPartitionKey(id),
                LatestPackageLeaf.GetRowKey(version));
        }

        internal async Task<TableClient> GetTableAsync()
        {
            return (await _serviceClientFactory.GetTableServiceClientAsync())
                .GetTableClient(_options.Value.LatestPackageLeafTableName);
        }
    }
}

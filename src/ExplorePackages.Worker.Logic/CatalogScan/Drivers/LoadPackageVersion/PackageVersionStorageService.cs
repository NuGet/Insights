using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Worker.LoadPackageVersion
{
    public class PackageVersionStorageService
    {
        private readonly NewServiceClientFactory _serviceClientFactory;
        private readonly CatalogClient _catalogClient;
        private readonly ITelemetryClient _telemetryClient;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;

        public PackageVersionStorageService(
            NewServiceClientFactory serviceClientFactory,
            CatalogClient catalogClient,
            ITelemetryClient telemetryClient,
            IOptions<ExplorePackagesWorkerSettings> options)
        {
            _serviceClientFactory = serviceClientFactory;
            _catalogClient = catalogClient;
            _telemetryClient = telemetryClient;
            _options = options;
        }

        public async Task InitializeAsync()
        {
            await (await GetTableAsync()).CreateIfNotExistsAsync();
        }

        public async Task<PackageVersionStorage> GetLatestPackageLeafStorageAsync()
        {
            return new PackageVersionStorage(await GetTableAsync(), _catalogClient);
        }

        public async Task<IReadOnlyList<PackageVersionEntity>> GetAsync(string packageId)
        {
            return await (await GetTableAsync())
                .QueryAsync<PackageVersionEntity>(x => x.PartitionKey == PackageVersionEntity.GetPartitionKey(packageId))
                .ToListAsync(_telemetryClient.StartQueryLoopMetrics());
        }

        internal async Task<TableClient> GetTableAsync()
        {
            return (await _serviceClientFactory.GetTableServiceClientAsync())
                .GetTableClient(_options.Value.PackageVersionTableName);
        }
    }
}

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.Worker.LoadPackageVersion
{
    public class PackageVersionStorageService : ILatestPackageLeafStorage<PackageVersionEntity>
    {
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly CatalogClient _catalogClient;
        private readonly ITelemetryClient _telemetryClient;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;

        public PackageVersionStorageService(
            ServiceClientFactory serviceClientFactory,
            CatalogClient catalogClient,
            ITelemetryClient telemetryClient,
            IOptions<ExplorePackagesWorkerSettings> options)
        {
            _serviceClientFactory = serviceClientFactory;
            _catalogClient = catalogClient;
            _telemetryClient = telemetryClient;
            _options = options;

            Table = _serviceClientFactory
                .GetStorageAccount()
                .CreateCloudTableClient()
                .GetTableReference(_options.Value.PackageVersionTableName);
        }

        public CloudTable Table { get; }
        public string CommitTimestampColumnName => nameof(PackageVersionEntity.CommitTimestamp);

        public async Task InitializeAsync()
        {
            await Table.CreateIfNotExistsAsync(retry: true);
        }

        public string GetPartitionKey(string packageId)
        {
            return PackageVersionEntity.GetPartitionKey(packageId);
        }

        public string GetRowKey(string packageVersion)
        {
            return PackageVersionEntity.GetRowKey(packageVersion);
        }

        public async Task<IReadOnlyList<PackageVersionEntity>> GetAsync(string packageId)
        {
            return await Table.GetEntitiesAsync<PackageVersionEntity>(
                GetPartitionKey(packageId),
                _telemetryClient.StartQueryLoopMetrics());
        }

        public async Task<PackageVersionEntity> MapAsync(CatalogLeafItem item)
        {
            if (item.Type == CatalogLeafType.PackageDelete)
            {
                return new PackageVersionEntity(
                    item,
                    created: null,
                    listed: null,
                    semVerType: null);
            }

            var leaf = (PackageDetailsCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(item);

            return new PackageVersionEntity(
                item,
                leaf.Created,
                leaf.IsListed(),
                leaf.GetSemVerType());
        }
    }
}

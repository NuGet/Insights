using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.Worker.LoadPackageVersion
{
    public class PackageVersionStorage : ILatestPackageLeafStorage<PackageVersionEntity>
    {
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly CatalogClient _catalogClient;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;

        public PackageVersionStorage(
            ServiceClientFactory serviceClientFactory,
            CatalogClient catalogClient,
            IOptions<ExplorePackagesWorkerSettings> options)
        {
            _serviceClientFactory = serviceClientFactory;
            _catalogClient = catalogClient;
            _options = options;

            Table = _serviceClientFactory
                .GetStorageAccount()
                .CreateCloudTableClient()
                .GetTableReference(_options.Value.PackageVersionTableName);
        }

        public CloudTable Table { get; }
        public string CommitTimestampColumnName => nameof(PackageVersionEntity.CommitTimestamp);

        public string GetPartitionKey(string packageId)
        {
            return PackageVersionEntity.GetPartitionKey(packageId);
        }

        public string GetRowKey(string packageVersion)
        {
            return PackageVersionEntity.GetRowKey(packageVersion);
        }

        public async Task<PackageVersionEntity> MapAsync(CatalogLeafItem item)
        {
            if (item.Type == CatalogLeafType.PackageDelete)
            {
                return new PackageVersionEntity(
                    item,
                    listed: null,
                    semVerType: null);
            }

            var leaf = (PackageDetailsCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(item);

            return new PackageVersionEntity(
                item,
                leaf.IsListed(),
                leaf.GetSemVerType());
        }
    }
}

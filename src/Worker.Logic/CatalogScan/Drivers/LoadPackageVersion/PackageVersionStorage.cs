using System.Threading.Tasks;
using Azure.Data.Tables;

namespace Knapcode.ExplorePackages.Worker.LoadPackageVersion
{
    public class PackageVersionStorage : ILatestPackageLeafStorage<PackageVersionEntity>
    {
        private readonly CatalogClient _catalogClient;

        public PackageVersionStorage(
            TableClient tableClient,
            CatalogClient catalogClient)
        {
            Table = tableClient;
            _catalogClient = catalogClient;
        }

        public TableClient Table { get; }
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

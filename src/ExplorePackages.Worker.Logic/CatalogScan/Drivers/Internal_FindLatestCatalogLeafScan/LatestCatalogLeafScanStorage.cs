using System.Threading.Tasks;
using Azure.Data.Tables;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Worker.FindLatestCatalogLeafScan
{
    public class LatestCatalogLeafScanStorage : ILatestPackageLeafStorage<CatalogLeafScan>
    {
        private readonly CatalogIndexScan _indexScan;

        public LatestCatalogLeafScanStorage(TableClient table, CatalogIndexScan indexScan)
        {
            Table = table;
            _indexScan = indexScan;
        }

        public TableClient Table { get; }
        public string CommitTimestampColumnName => nameof(CatalogLeafScan.CommitTimestamp);

        public string GetPartitionKey(string packageId)
        {
            return CatalogLeafScan.GetPartitionKey(_indexScan.ScanId, GetPageId(packageId));
        }

        public string GetRowKey(string packageVersion)
        {
            return GetLeafId(packageVersion);
        }

        public Task<CatalogLeafScan> MapAsync(CatalogLeafItem item)
        {
            return Task.FromResult(new CatalogLeafScan(_indexScan.StorageSuffix, _indexScan.ScanId, GetPageId(item.PackageId), GetLeafId(item.PackageVersion))
            {
                ParsedDriverType = _indexScan.ParsedDriverType,
                DriverParameters = _indexScan.DriverParameters,
                Url = item.Url,
                ParsedLeafType = item.Type,
                CommitId = item.CommitId,
                CommitTimestamp = item.CommitTimestamp,
                PackageId = item.PackageId,
                PackageVersion = item.PackageVersion,
            });
        }

        private static string GetPageId(string packageId)
        {
            return packageId.ToLowerInvariant();
        }

        private static string GetLeafId(string packageVersion)
        {
            return NuGetVersion.Parse(packageVersion).ToNormalizedString().ToLowerInvariant();
        }
    }
}

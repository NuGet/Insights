using Microsoft.WindowsAzure.Storage.Table;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Worker.FindLatestCatalogLeafScans
{
    public class LatestCatalogLeafScansStorage : ILatestPackageLeafStorage<CatalogLeafScan>
    {
        private readonly CatalogIndexScan _indexScan;

        public LatestCatalogLeafScansStorage(CloudTable table, CatalogIndexScan indexScan)
        {
            Table = table;
            _indexScan = indexScan;
        }

        public CloudTable Table { get; }
        public string CommitTimestampColumnName => nameof(CatalogLeafScan.CommitTimestamp);
        public string GetPartitionKey(string packageId) => CatalogLeafScan.GetPartitionKey(_indexScan.ScanId, GetPageId(packageId));
        public string GetRowKey(string packageVersion) => GetLeafId(packageVersion);

        public CatalogLeafScan Map(CatalogLeafItem item)
        {
            return new CatalogLeafScan(_indexScan.StorageSuffix, _indexScan.ScanId, GetPageId(item.PackageId), GetLeafId(item.PackageVersion))
            {
                ParsedDriverType = _indexScan.ParsedDriverType,
                DriverParameters = _indexScan.DriverParameters,
                Url = item.Url,
                ParsedLeafType = item.Type,
                CommitId = item.CommitId,
                CommitTimestamp = item.CommitTimestamp,
                PackageId = item.PackageId,
                PackageVersion = item.PackageVersion,
            };
        }

        private static string GetPageId(string packageId) => packageId.ToLowerInvariant();
        private static string GetLeafId(string packageVersion) => NuGetVersion.Parse(packageVersion).ToNormalizedString().ToLowerInvariant();
    }
}

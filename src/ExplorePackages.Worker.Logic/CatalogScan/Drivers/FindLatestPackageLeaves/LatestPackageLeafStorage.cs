using System.Collections.Generic;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.Worker.FindLatestPackageLeaves
{
    public class LatestPackageLeafStorage : ILatestPackageLeafStorage<LatestPackageLeaf>
    {
        private readonly string _prefix;
        private readonly IReadOnlyDictionary<CatalogLeafItem, int> _leafItemToRank;
        private readonly int _pageRank;
        private readonly string _pageUrl;

        public LatestPackageLeafStorage(
            CloudTable table,
            string prefix,
            IReadOnlyDictionary<CatalogLeafItem, int> leafItemToRank,
            int pageRank,
            string pageUrl)
        {
            Table = table;
            _prefix = prefix;
            _leafItemToRank = leafItemToRank;
            _pageRank = pageRank;
            _pageUrl = pageUrl;
        }

        public CloudTable Table { get; }
        public string GetPartitionKey(string packageId) => LatestPackageLeaf.GetPartitionKey(_prefix, packageId);
        public string GetRowKey(string packageVersion) => LatestPackageLeaf.GetRowKey(packageVersion);
        public string CommitTimestampColumnName => nameof(LatestPackageLeaf.CommitTimestamp);

        public LatestPackageLeaf Map(CatalogLeafItem item)
        {
            return new LatestPackageLeaf(_prefix, item, _leafItemToRank[item], _pageRank, _pageUrl);
        }
    }
}

using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Data.Tables;

namespace Knapcode.ExplorePackages.Worker.LoadLatestPackageLeaf
{
    public class LatestPackageLeafStorage : ILatestPackageLeafStorage<LatestPackageLeaf>
    {
        private readonly IReadOnlyDictionary<CatalogLeafItem, int> _leafItemToRank;
        private readonly int _pageRank;
        private readonly string _pageUrl;

        public LatestPackageLeafStorage(
            TableClient table,
            IReadOnlyDictionary<CatalogLeafItem, int> leafItemToRank,
            int pageRank,
            string pageUrl)
        {
            Table = table;
            _leafItemToRank = leafItemToRank;
            _pageRank = pageRank;
            _pageUrl = pageUrl;
        }

        public TableClient Table { get; }

        public string GetPartitionKey(string packageId)
        {
            return LatestPackageLeaf.GetPartitionKey(packageId);
        }

        public string GetRowKey(string packageVersion)
        {
            return LatestPackageLeaf.GetRowKey(packageVersion);
        }

        public string CommitTimestampColumnName => nameof(LatestPackageLeaf.CommitTimestamp);

        public Task<LatestPackageLeaf> MapAsync(CatalogLeafItem item)
        {
            return Task.FromResult(new LatestPackageLeaf(
                item,
                _leafItemToRank[item],
                _pageRank,
                _pageUrl));
        }
    }
}

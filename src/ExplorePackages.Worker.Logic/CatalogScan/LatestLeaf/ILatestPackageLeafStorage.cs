using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.Worker
{
    public interface ILatestPackageLeafStorage<T> where T : ILatestPackageLeaf
    {
        CloudTable Table { get; }
        Task<T> MapAsync(CatalogLeafItem item);
        string GetPartitionKey(string packageId);
        string GetRowKey(string packageVersion);
        string CommitTimestampColumnName { get; }
    }
}

using System.Threading.Tasks;
using Azure.Data.Tables;

namespace NuGet.Insights.Worker
{
    public interface ILatestPackageLeafStorage<T> where T : ILatestPackageLeaf
    {
        TableClient Table { get; }
        Task<T> MapAsync(CatalogLeafItem item);
        string GetPartitionKey(string packageId);
        string GetRowKey(string packageVersion);
        string CommitTimestampColumnName { get; }
    }
}

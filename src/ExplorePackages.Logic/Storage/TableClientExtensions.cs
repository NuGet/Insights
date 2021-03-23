using System.Net;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;

namespace Knapcode.ExplorePackages
{
    public static class TableClientExtensions
    {
        public static async Task<T> GetEntityOrNullAsync<T>(this TableClient table, string partitionKey, string rowKey) where T : class, ITableEntity, new()
        {
            try
            {
                return await table.GetEntityAsync<T>(partitionKey, rowKey);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                return null;
            }
        }
    }
}

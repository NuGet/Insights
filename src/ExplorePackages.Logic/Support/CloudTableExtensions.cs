using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.Logic
{
    public static class CloudTableExtensions
    {
        public static async Task CreateIfNotExistsAsync(this CloudTable table, bool retry)
        {
            do
            {
                try
                {
                    await table.CreateIfNotExistsAsync();
                    return;
                }
                catch (StorageException ex) when (
                    ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.Conflict
                    && ex.RequestInformation?.ExtendedErrorInformation?.ErrorCode == "TableBeingDeleted")
                {
                    // Retry in this case.
                }
            }
            while (retry);
        }
    }
}

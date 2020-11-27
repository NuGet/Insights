using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Blob.Protocol;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Queue.Protocol;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Table.Protocol;

namespace Knapcode.ExplorePackages
{
    public static class CloudExtensions
    {
        private static readonly TimeSpan MaxRetryDuration = TimeSpan.FromMinutes(5);

        public static async Task CreateIfNotExistsAsync(this CloudBlobContainer table, bool retry)
        {
            await CreateIfNotExistsAsync(
                () => table.CreateIfNotExistsAsync(),
                retry,
                BlobErrorCodeStrings.ContainerBeingDeleted);
        }

        public static async Task CreateIfNotExistsAsync(this CloudQueue queue, bool retry)
        {
            await CreateIfNotExistsAsync(
                () => queue.CreateIfNotExistsAsync(),
                retry,
                QueueErrorCodeStrings.QueueBeingDeleted);
        }

        public static async Task CreateIfNotExistsAsync(this CloudTable table, bool retry)
        {
            await CreateIfNotExistsAsync(
                () => table.CreateIfNotExistsAsync(),
                retry,
                TableErrorCodeStrings.TableBeingDeleted);
        }

        public static async Task CreateIfNotExistsAsync(Func<Task> createIfNotExistsAsync, bool retry, string errorCode)
        {
            var stopwatch = Stopwatch.StartNew();
            do
            {
                try
                {
                    await createIfNotExistsAsync();
                    return;
                }
                catch (StorageException ex) when (
                    ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.Conflict
                    && ex.RequestInformation?.ExtendedErrorInformation?.ErrorCode == errorCode)
                {
                    // Retry in this case.
                }
            }
            while (retry && stopwatch.Elapsed < MaxRetryDuration);
        }
    }
}

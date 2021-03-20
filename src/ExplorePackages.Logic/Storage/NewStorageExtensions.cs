using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;

namespace Knapcode.ExplorePackages
{
    public static class NewStorageExtensions
    {
        private static readonly TimeSpan MaxRetryDuration = TimeSpan.FromMinutes(5);

        public static async Task CreateIfNotExistsAsync(this QueueClient queueClient, bool retry)
        {
            await CreateIfNotExistsAsync(
                () => queueClient.CreateIfNotExistsAsync(),
                retry,
                QueueErrorCode.QueueBeingDeleted.ToString());
        }

        private static async Task CreateIfNotExistsAsync(Func<Task> createIfNotExistsAsync, bool retry, string errorCode)
        {
            var stopwatch = Stopwatch.StartNew();
            var firstTime = true;
            do
            {
                if (!firstTime)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }

                firstTime = false;

                try
                {
                    await createIfNotExistsAsync();
                    return;
                }
                catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict && ex.ErrorCode == errorCode)
                {
                    // Retry in this case.
                }
            }
            while (retry && stopwatch.Elapsed < MaxRetryDuration);
        }
    }
}

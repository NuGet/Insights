// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using NuGet.Insights.StorageNoOpRetry;

namespace NuGet.Insights
{
    public static class StorageExtensions
    {
        internal static readonly TimeSpan MaxRetryDuration = TimeSpan.FromMinutes(5);

        public static async Task CreateIfNotExistsAsync(this QueueClient queueClient, bool retry)
        {
            await CreateIfNotExistsAsync(
                async () => await queueClient.ExistsAsync(),
                () => queueClient.CreateIfNotExistsAsync(),
                retry,
                QueueErrorCode.QueueBeingDeleted.ToString());
        }

        public static async Task CreateIfNotExistsAsync(this BlobContainerClient containerClient, bool retry)
        {
            await CreateIfNotExistsAsync(
                async () => await containerClient.ExistsAsync(),
                () => containerClient.CreateIfNotExistsAsync(),
                retry,
                BlobErrorCode.ContainerBeingDeleted.ToString());
        }

        public static async Task CreateIfNotExistsAsync(this TableClientWithRetryContext tableClient, bool retry)
        {
            await CreateIfNotExistsAsync(
                () => tableClient.ExistsAsync(),
                () => tableClient.CreateIfNotExistsAsync(),
                retry,
                "TableBeingDeleted");
        }

        private static async Task CreateIfNotExistsAsync(
            Func<Task<bool>> existsAsync, 
            Func<Task> createIfNotExistsAsync,
            bool retry,
            string errorCode)
        {
            if (await existsAsync())
            {
                return;
            }

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

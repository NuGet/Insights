// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

#nullable enable

namespace NuGet.Insights.StorageNoOpRetry
{
    public class BlobServiceClientWithRetryContext
    {
        private readonly BlobServiceClient _client;

        public BlobServiceClientWithRetryContext(BlobServiceClient client)
        {
            _client = client;
        }

        public BlobContainerClientWithRetryContext GetBlobContainerClient(string blobContainerName)
        {
            return new BlobContainerClientWithRetryContext(_client.GetBlobContainerClient(blobContainerName));
        }

        public AsyncPageable<BlobContainerItem> GetBlobContainersAsync(
            BlobContainerTraits traits = BlobContainerTraits.None,
            BlobContainerStates states = BlobContainerStates.None,
            string? prefix = default,
            CancellationToken cancellationToken = default)
        {
            return _client.GetBlobContainersAsync(traits, states, prefix, cancellationToken);
        }

        public Task<Response> DeleteBlobContainerAsync(
            string blobContainerName,
            BlobRequestConditions? conditions = default,
            CancellationToken cancellationToken = default)
        {
            return _client.DeleteBlobContainerAsync(blobContainerName, conditions, cancellationToken);
        }
    }
}

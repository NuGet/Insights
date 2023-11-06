// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Metadata = System.Collections.Generic.IDictionary<string, string>;

#nullable enable

namespace NuGet.Insights.StorageNoOpRetry
{
    public class BlobContainerClientWithRetryContext
    {
        private readonly BlobContainerClient _client;

        public BlobContainerClientWithRetryContext(BlobContainerClient client)
        {
            _client = client;
        }

        protected internal BlockBlobClientWithRetryContext GetBlockBlobClientCore(string blobName)
        {
            return new BlockBlobClientWithRetryContext(_client.GetBlockBlobClient(blobName));
        }

        public Task<Response<bool>> ExistsAsync(CancellationToken cancellationToken = default)
        {
            return _client.ExistsAsync(cancellationToken);
        }

        public Task<Response<bool>> DeleteIfExistsAsync(
            BlobRequestConditions? conditions = default,
            CancellationToken cancellationToken = default)
        {
            return _client.DeleteIfExistsAsync(conditions, cancellationToken);
        }

        public Task<Response<BlobContainerInfo>> CreateIfNotExistsAsync(
            PublicAccessType publicAccessType = PublicAccessType.None,
            Metadata? metadata = default,
            BlobContainerEncryptionScopeOptions? encryptionScopeOptions = default,
            CancellationToken cancellationToken = default)
        {
            return _client.CreateIfNotExistsAsync(publicAccessType, metadata, encryptionScopeOptions, cancellationToken);
        }

        public BlobClientWithRetryContext GetBlobClient(string name)
        {
            return new BlobClientWithRetryContext(_client.GetBlobClient(name));
        }

        public AsyncPageable<BlobItem> GetBlobsAsync(
            BlobTraits traits = BlobTraits.None,
            BlobStates states = BlobStates.None,
            string? prefix = default,
            CancellationToken cancellationToken = default)
        {
            return _client.GetBlobsAsync(traits, states, prefix, cancellationToken);
        }
    }
}

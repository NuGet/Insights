// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

#nullable enable

namespace NuGet.Insights.MemoryStorage
{
    public partial class MemoryBlobContainerClient : BlobContainerClient
    {
        private readonly MemoryBlobContainerStore _store;
        private readonly TokenCredential _credential;
        private readonly BlobClientOptions _options;

        public MemoryBlobContainerClient(MemoryBlobServiceStore store, Uri uri, TokenCredential credential, BlobClientOptions options)
            : base(uri, credential, options.AddBrokenTransport())
        {
            _store = store.GetContainer(Name);
            _credential = credential;
            _options = options;
        }

        private Uri GetBlobUri(string blobName)
        {
            return new BlobUriBuilder(Uri, _options.TrimBlobNameSlashes) { BlobName = blobName }.ToUri();
        }

        public override BlobClient GetBlobClient(
            string blobName)
        {
            var uri = GetBlobUri(blobName);
            return new MemoryBlobClient(_store, uri, _credential, _options);
        }

        protected override BlockBlobClient GetBlockBlobClientCore(
            string blobName)
        {
            var uri = GetBlobUri(blobName);
            return new MemoryBlockBlobClient(_store, uri, _credential, _options);
        }

        public override Task<Response<BlobContainerInfo>?> CreateIfNotExistsAsync(
            PublicAccessType publicAccessType = PublicAccessType.None,
            IDictionary<string, string>? metadata = null,
            BlobContainerEncryptionScopeOptions? encryptionScopeOptions = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateIfNotExistsResponse(publicAccessType, metadata, encryptionScopeOptions));
        }

        public override Task<Response> DeleteAsync(
            BlobRequestConditions? conditions = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(DeleteResponse(conditions));
        }

        public override AsyncPageable<BlobItem> GetBlobsAsync(
            BlobTraits traits = BlobTraits.None,
            BlobStates states = BlobStates.None,
            string? prefix = null,
            CancellationToken cancellationToken = default)
        {
            return AsyncPageable<BlobItem>.FromPages(GetBlobPages(traits, states, prefix));
        }

        public override Task<Response<bool>> DeleteIfExistsAsync(
            BlobRequestConditions? conditions = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(DeleteIfExistsResponse(conditions));
        }

        public override Task<Response<bool>> ExistsAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExistsResponse());
        }
    }
}

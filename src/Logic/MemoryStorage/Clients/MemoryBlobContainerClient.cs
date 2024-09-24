// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using Azure.Core;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

#nullable enable

namespace NuGet.Insights.MemoryStorage
{
    public partial class MemoryBlobContainerClient : BlobContainerClient
    {
        private readonly StorageSharedKeyCredential? _sharedKeyCredential;
        private readonly TokenCredential? _tokenCredential;

        public MemoryBlobContainerClient(MemoryBlobServiceClient parent, Uri uri, StorageSharedKeyCredential credential)
            : base(uri, credential, parent.Options)
        {
            _sharedKeyCredential = credential;
            Options = parent.Options;
            Parent = parent;
            Store = parent.Store.GetContainer(Name);
        }

        public MemoryBlobContainerClient(MemoryBlobServiceClient parent, Uri uri, TokenCredential credential)
            : base(uri, credential, parent.Options)
        {
            _tokenCredential = credential;
            Options = parent.Options;
            Parent = parent;
            Store = parent.Store.GetContainer(Name);
        }

        public BlobClientOptions Options { get; }
        public MemoryBlobServiceClient Parent { get; }
        public MemoryBlobContainerStore Store { get; }

        private Uri GetBlobUri(string blobName)
        {
            return new BlobUriBuilder(Uri, Options.TrimBlobNameSlashes) { BlobName = blobName }.ToUri();
        }

        public override BlobClient GetBlobClient(
            string blobName)
        {
            var uri = GetBlobUri(blobName);
            if (_sharedKeyCredential is not null)
            {
                return new MemoryBlobClient(this, uri, _sharedKeyCredential);
            }
            else
            {
                return new MemoryBlobClient(this, uri, _tokenCredential!);
            }
        }

        protected override BlockBlobClient GetBlockBlobClientCore(
            string blobName)
        {
            var uri = GetBlobUri(blobName);
            if (_sharedKeyCredential is not null)
            {
                return new MemoryBlockBlobClient(this, uri, _sharedKeyCredential);
            }
            else
            {
                return new MemoryBlockBlobClient(this, uri, _tokenCredential!);
            }
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

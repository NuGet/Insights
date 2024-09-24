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
    public partial class MemoryBlobClient : BlobClient
    {
        private readonly StorageSharedKeyCredential? _sharedKeyCredential;
        private readonly TokenCredential? _tokenCredential;

        public MemoryBlobClient(MemoryBlobContainerClient parent, Uri blobUri, StorageSharedKeyCredential credential)
            : base(blobUri, credential, parent.Options)
        {
            _sharedKeyCredential = credential;
            Options = parent.Options;
            Parent = parent;
            Store = parent.Store.GetBlob(Name);
        }

        public MemoryBlobClient(MemoryBlobContainerClient parent, Uri blobUri, TokenCredential credential)
            : base(blobUri, credential, parent.Options)
        {
            _tokenCredential = credential;
            Options = parent.Options;
            Parent = parent;
            Store = parent.Store.GetBlob(Name);
        }

        public BlobClientOptions Options { get; }
        public MemoryBlobContainerClient Parent { get; }
        public MemoryBlobStore Store { get; }

        protected override BlobLeaseClient GetBlobLeaseClientCore(
            string? leaseId)
        {
            return new MemoryBlobLeaseClient(this, leaseId);
        }

        public override Task<Response<BlobProperties>> GetPropertiesAsync(
            BlobRequestConditions? conditions = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Store.GetPropertiesResponse(conditions));
        }

        public override Task<CopyFromUriOperation> StartCopyFromUriAsync(
            Uri source,
            BlobCopyFromUriOptions options,
            CancellationToken cancellationToken = default)
        {
            MemoryBlobClient sourceClient;
            if (_sharedKeyCredential is not null)
            {
                sourceClient = new MemoryBlobClient(Parent, source, _sharedKeyCredential);
            }
            else
            {
                sourceClient = new MemoryBlobClient(Parent, source, _tokenCredential!);
            }

            if (sourceClient.Uri.Authority != Uri.Authority)
            {
                throw new NotImplementedException();
            }

            var id = $"blob-copy-id-{Guid.NewGuid()}";
            var result = Store.CopyFrom(sourceClient.Store, id, options);
            return result switch
            {
                StorageResultType.Success => Task.FromResult(new CopyFromUriOperation(id, this)),
                _ => throw new NotImplementedException("Unexpected result type: " + result),
            };
        }

        public override Task<Response<BlobInfo>> SetMetadataAsync(
            IDictionary<string, string> metadata,
            BlobRequestConditions? conditions = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Store.SetMetadataResponse(metadata, conditions));
        }

        public override Task<Response<BlobDownloadStreamingResult>> DownloadStreamingAsync(
            BlobDownloadOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Store.DownloadStreamingResponse(options));
        }

        public override Task<Response> DownloadToAsync(
            Stream destination,
            BlobRequestConditions? conditions = null,
            StorageTransferOptions transferOptions = default,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Store.DownloadToResponse(destination, conditions, transferOptions, cancellationToken));
        }

        public override Task<Response<BlobContentInfo>> UploadAsync(
            Stream content,
            BlobUploadOptions options,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Store.UploadResponse(content, options));
        }

        public override Task<Response<BlobContentInfo>> UploadAsync(
            Stream content,
            BlobHttpHeaders? httpHeaders = null,
            IDictionary<string, string>? metadata = null,
            BlobRequestConditions? conditions = null,
            IProgress<long>? progressHandler = null,
            AccessTier? accessTier = null,
            StorageTransferOptions transferOptions = default,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Store.UploadResponse(content, httpHeaders, metadata, conditions, progressHandler, accessTier, transferOptions));
        }

        public override Task<Response> DeleteAsync(
            DeleteSnapshotsOption snapshotsOption = DeleteSnapshotsOption.None,
            BlobRequestConditions? conditions = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Store.DeleteResponse(snapshotsOption, conditions));
        }
    }
}

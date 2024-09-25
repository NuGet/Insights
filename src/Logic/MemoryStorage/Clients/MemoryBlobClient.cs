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
        private readonly MemoryBlobContainerStore _parent;
        private readonly MemoryBlobStore _store;
        private readonly BlobClientOptions _options;

        public MemoryBlobClient(MemoryBlobContainerStore parent, Uri blobUri, TokenCredential tokenCredential, BlobClientOptions options)
            : base(blobUri, tokenCredential, options.AddBrokenTransport())
        {
            _parent = parent;
            _store = parent.GetBlob(Name);
            _options = options;
        }

        protected override BlobLeaseClient GetBlobLeaseClientCore(
            string? leaseId)
        {
            return new MemoryBlobLeaseClient(_store, this, leaseId);
        }

        public override Task<Response<BlobProperties>> GetPropertiesAsync(
            BlobRequestConditions? conditions = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_store.GetPropertiesResponse(conditions));
        }

        public override Task<CopyFromUriOperation> StartCopyFromUriAsync(
            Uri source,
            BlobCopyFromUriOptions options,
            CancellationToken cancellationToken = default)
        {
            var sourceBuilder = new BlobUriBuilder(source, _options.TrimBlobNameSlashes);
            var destBuilder = new BlobUriBuilder(Uri, _options.TrimBlobNameSlashes);
            if (!string.IsNullOrEmpty(sourceBuilder.VersionId)
                || !string.IsNullOrEmpty(sourceBuilder.Snapshot)
                || string.IsNullOrEmpty(sourceBuilder.BlobContainerName)
                || !string.IsNullOrEmpty(destBuilder.VersionId)
                || !string.IsNullOrEmpty(destBuilder.Snapshot)
                || string.IsNullOrEmpty(destBuilder.BlobName)
                || destBuilder.BlobName != Name)
            {
                throw new NotImplementedException();
            }

            var sourceBlobName = sourceBuilder.BlobName;
            sourceBuilder.BlobName = null;
            destBuilder.BlobName = null;
            if (sourceBuilder.ToUri() != destBuilder.ToUri())
            {
                throw new NotImplementedException();
            }

            var sourceStore = _parent.GetBlob(sourceBlobName);
            var id = $"blob-copy-id-{Guid.NewGuid()}";
            var result = _store.CopyFrom(sourceStore, id, options);
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
            return Task.FromResult(_store.SetMetadataResponse(metadata, conditions));
        }

        public override Task<Response<BlobDownloadStreamingResult>> DownloadStreamingAsync(
            BlobDownloadOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_store.DownloadStreamingResponse(options));
        }

        public override Task<Response> DownloadToAsync(
            Stream destination,
            BlobRequestConditions? conditions = null,
            StorageTransferOptions transferOptions = default,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_store.DownloadToResponse(destination, conditions, transferOptions, cancellationToken));
        }

        public override Task<Response<BlobContentInfo>> UploadAsync(
            Stream content,
            BlobUploadOptions options,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_store.UploadResponse(content, options));
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
            return Task.FromResult(_store.UploadResponse(content, httpHeaders, metadata, conditions, progressHandler, accessTier, transferOptions));
        }

        public override Task<Response> DeleteAsync(
            DeleteSnapshotsOption snapshotsOption = DeleteSnapshotsOption.None,
            BlobRequestConditions? conditions = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_store.DeleteResponse(snapshotsOption, conditions));
        }
    }
}

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
    public partial class MemoryBlockBlobClient : BlockBlobClient
    {
        private readonly StorageSharedKeyCredential? _sharedKeyCredential;
        private readonly TokenCredential? _tokenCredential;

        public MemoryBlockBlobClient(MemoryBlobContainerClient parent, Uri blobUri, StorageSharedKeyCredential credential)
            : base(blobUri, credential, parent.Options)
        {
            _sharedKeyCredential = credential;
            Options = parent.Options;
            Parent = parent;
            Store = parent.Store.GetBlob(Name);
        }

        public MemoryBlockBlobClient(MemoryBlobContainerClient parent, Uri blobUri, TokenCredential credential)
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

        public override Task<Response<BlobProperties>> GetPropertiesAsync(
            BlobRequestConditions? conditions = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Store.GetPropertiesResponse(conditions));
        }

        public override Task<Response<BlobDownloadStreamingResult>> DownloadStreamingAsync(
            BlobDownloadOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Store.DownloadStreamingResponse(options));
        }

        public override Task<Response<BlobDownloadResult>> DownloadContentAsync(BlobRequestConditions conditions, CancellationToken cancellationToken)
        {
            return Task.FromResult(Store.DownloadContentResponse(conditions));
        }

        public override Task<Response<BlobContentInfo>> UploadAsync(
            Stream content,
            BlobUploadOptions options,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Store.UploadResponse(content, options));
        }
    }
}

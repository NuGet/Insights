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
    public partial class MemoryBlockBlobClient : BlockBlobClient
    {
        private readonly MemoryBlobStore _store;

        public MemoryBlockBlobClient(MemoryBlobContainerStore parent, Uri blobUri, TokenCredential tokenCredential, BlobClientOptions options)
            : base(blobUri, tokenCredential, options.AddBrokenTransport())
        {
            _store = parent.GetBlob(Name);
        }

        public override Task<Response<BlobProperties>> GetPropertiesAsync(
            BlobRequestConditions? conditions = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_store.GetPropertiesResponse(conditions));
        }

        public override Task<Response<BlobDownloadStreamingResult>> DownloadStreamingAsync(
            BlobDownloadOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_store.DownloadStreamingResponse(options));
        }

        public override Task<Response<BlobDownloadResult>> DownloadContentAsync(BlobRequestConditions conditions, CancellationToken cancellationToken)
        {
            return Task.FromResult(_store.DownloadContentResponse(conditions));
        }

        public override Task<Response<BlobContentInfo>> UploadAsync(
            Stream content,
            BlobUploadOptions options,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_store.UploadResponse(content, options));
        }
    }
}

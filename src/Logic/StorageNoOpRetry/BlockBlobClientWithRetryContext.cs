// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

#nullable enable

namespace NuGet.Insights.StorageNoOpRetry
{
    public class BlockBlobClientWithRetryContext : BlobBaseClientWithRetryContext
    {
        private readonly BlockBlobClient _client;

        public BlockBlobClientWithRetryContext(BlockBlobClient client) : base(client)
        {
            _client = client;
        }

        public Task<Stream> OpenWriteAsync(
            bool overwrite,
            BlockBlobOpenWriteOptions? options = default,
            CancellationToken cancellationToken = default)
        {
            return _client.OpenWriteAsync(overwrite, options, cancellationToken);
        }

        public Task<Response<BlobCopyInfo>> SyncCopyFromUriAsync(
            Uri source,
            BlobCopyFromUriOptions? options = default,
            CancellationToken cancellationToken = default)
        {
            return _client.SyncCopyFromUriAsync(source, options, cancellationToken);
        }
    }
}

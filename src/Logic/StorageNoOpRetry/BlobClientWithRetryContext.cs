// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

#nullable enable

namespace NuGet.Insights.StorageNoOpRetry
{
    public class BlobClientWithRetryContext : BlobBaseClientWithRetryContext
    {
        private readonly BlobClient _client;

        public BlobClientWithRetryContext(BlobClient client) : base(client)
        {
            _client = client;
        }

        public Task<Response<BlobContentInfo>> UploadAsync(
            Stream content,
            BlobUploadOptions options,
            CancellationToken cancellationToken = default)
        {
            return _client.UploadAsync(content, options, cancellationToken);
        }
    }
}

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
    public class BlobBaseClientWithRetryContext
    {
        private readonly BlobBaseClient _client;

        public BlobBaseClientWithRetryContext(BlobBaseClient client)
        {
            _client = client;
        }

        public string Name => _client.Name;
        public string BlobContainerName => _client.BlobContainerName;
        public Uri Uri => _client.Uri;

        public Task<Response> DeleteAsync(
            DeleteSnapshotsOption snapshotsOption = default,
            BlobRequestConditions? conditions = default,
            CancellationToken cancellationToken = default)
        {
            return _client.DeleteAsync(snapshotsOption, conditions, cancellationToken);
        }

        public Task<Response> DownloadToAsync(Stream destination)
        {
            return _client.DownloadToAsync(destination);
        }

        public Task<CopyFromUriOperation> StartCopyFromUriAsync(
            Uri source,
            BlobCopyFromUriOptions options,
            CancellationToken cancellationToken = default)
        {
            return _client.StartCopyFromUriAsync(source, options, cancellationToken);
        }

        public Task<Response<BlobDownloadInfo>> DownloadAsync()
        {
            return _client.DownloadAsync();
        }

        public Task<Response<BlobProperties>> GetPropertiesAsync(
            BlobRequestConditions? conditions = default,
            CancellationToken cancellationToken = default)
        {
            return _client.GetPropertiesAsync(conditions, cancellationToken);
        }
    }
}

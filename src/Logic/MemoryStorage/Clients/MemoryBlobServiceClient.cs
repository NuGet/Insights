// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using Azure.Core;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

#nullable enable

namespace NuGet.Insights.MemoryStorage
{
    public partial class MemoryBlobServiceClient : BlobServiceClient
    {
        private static readonly string LatestServiceVersion = new BlobClientOptions()
            .Version
            .ToString()
            .Replace("v", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("_", "-", StringComparison.Ordinal);

        private static readonly MemoryBlobServiceStore SharedStore = new MemoryBlobServiceStore();

        private readonly StorageSharedKeyCredential? _sharedKeyCredential;
        private readonly TokenCredential? _tokenCredential;

        public MemoryBlobServiceClient(
            Uri serviceUri,
            StorageSharedKeyCredential credential,
            BlobClientOptions options) : base(serviceUri, credential, options.AddBrokenTransport())
        {
            _sharedKeyCredential = credential;
            Options = options;
        }

        public MemoryBlobServiceClient(
            Uri serviceUri,
            TokenCredential credential,
            BlobClientOptions options) : base(serviceUri, credential, options.AddBrokenTransport())
        {
            _tokenCredential = credential;
            Options = options;
        }

        public BlobClientOptions Options { get; }
        public MemoryBlobServiceStore Store { get; } = SharedStore;

        public override BlobContainerClient GetBlobContainerClient(
            string blobContainerName)
        {
            var uri = Uri.AppendToPath(blobContainerName);
            if (_sharedKeyCredential is not null)
            {
                return new MemoryBlobContainerClient(this, uri, _sharedKeyCredential);
            }
            else
            {
                return new MemoryBlobContainerClient(this, uri, _tokenCredential!);
            }
        }

        public override AsyncPageable<BlobContainerItem> GetBlobContainersAsync(
            BlobContainerTraits traits = BlobContainerTraits.None,
            BlobContainerStates states = BlobContainerStates.None,
            string? prefix = null,
            CancellationToken cancellationToken = default)
        {
            return AsyncPageable<BlobContainerItem>.FromPages(GetContainerPages(traits, states, prefix));
        }

        public override Task<Response<UserDelegationKey>> GetUserDelegationKeyAsync(
            DateTimeOffset? startsOn,
            DateTimeOffset expiresOn,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(GetUserDelegationKeyResponse(startsOn, expiresOn));
        }
    }
}

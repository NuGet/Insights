// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

#nullable enable

namespace NuGet.Insights.MemoryStorage
{
    public partial class MemoryBlobServiceClient : BlobServiceClient
    {
        private readonly TimeProvider _timeProvider;
        private readonly MemoryBlobServiceStore _store;
        private TokenCredential _credential;
        private readonly BlobClientOptions _options;

        public MemoryBlobServiceClient(TimeProvider timeProvider, MemoryBlobServiceStore store) : this(
            timeProvider,
            store,
            StorageUtility.GetBlobEndpoint(StorageUtility.MemoryStorageAccountName),
            new MemoryTokenCredential(timeProvider),
            new BlobClientOptions().AddBrokenTransport())
        {
        }

        private MemoryBlobServiceClient(
            TimeProvider timeProvider,
            MemoryBlobServiceStore store,
            Uri serviceUri,
            TokenCredential credential,
            BlobClientOptions options)
            : base(serviceUri, credential, options.AddBrokenTransport())
        {
            _timeProvider = timeProvider;
            _store = store;
            _credential = credential;
            _options = options;
        }

        public override BlobContainerClient GetBlobContainerClient(
            string blobContainerName)
        {
            var uri = Uri.AppendToPath(blobContainerName);
            return new MemoryBlobContainerClient(_store, uri, _credential, _options);
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

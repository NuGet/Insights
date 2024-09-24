// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

#nullable enable

namespace NuGet.Insights.MemoryStorage
{
    public partial class MemoryBlobServiceClient : BlobServiceClient
    {
        private IEnumerable<Page<BlobContainerItem>> GetContainerPages(
            BlobContainerTraits traits,
            BlobContainerStates states,
            string? prefix)
        {
            const int maxPerPageValue = StorageUtility.MaxTakeCount;
            return Store
                .GetContainerItems(traits, states, prefix)
                .Chunk(maxPerPageValue)
                .Select((x, i) => Page<BlobContainerItem>.FromValues(
                    x,
                    continuationToken: x.Length == maxPerPageValue ? $"container-item-page-{i}" : null,
                    new MemoryResponse(HttpStatusCode.OK)));
        }

        private Response<UserDelegationKey> GetUserDelegationKeyResponse(DateTimeOffset? startsOn, DateTimeOffset expiresOn)
        {
            var bytes = new byte[32];
            Random.Shared.NextBytes(bytes);
            var value = Convert.ToBase64String(bytes);
            var prefix = "testdelegationkeyinmemory";
            value = prefix + value.Substring(prefix.Length);

            return Response.FromValue(
                BlobsModelFactory.UserDelegationKey(
                    nameof(UserDelegationKey.SignedObjectId),
                    nameof(UserDelegationKey.SignedTenantId),
                    startsOn ?? DateTimeOffset.UtcNow,
                    expiresOn,
                    signedService: "b",
                    LatestServiceVersion,
                    value: value),
                new MemoryResponse(HttpStatusCode.OK));
        }
    }
}

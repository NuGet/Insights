// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

#nullable enable

namespace NuGet.Insights.MemoryStorage
{
    public partial class MemoryBlobContainerClient : BlobContainerClient
    {
        private IEnumerable<Page<BlobItem>> GetBlobPages(BlobTraits traits, BlobStates states, string? prefix)
        {
            var result = _store.GetBlobItems(traits, states, prefix);
            if (result.Type != StorageResultType.Success)
            {
                throw result.Type switch
                {
                    StorageResultType.DoesNotExist => new RequestFailedException(new MemoryResponse(HttpStatusCode.NotFound)),
                    _ => new NotImplementedException("Unexpected result type: " + result.Type),
                };
            }

            const int maxPerPageValue = StorageUtility.MaxTakeCount;
            return result
                .Value
                .Chunk(maxPerPageValue)
                .Select((x, i) => Page<BlobItem>.FromValues(
                    x,
                    continuationToken: x.Length == maxPerPageValue ? $"blob-item-page-{i}" : null,
                    new MemoryResponse(HttpStatusCode.OK)));
        }

        private Response<BlobContainerInfo>? CreateIfNotExistsResponse(
            PublicAccessType publicAccessType = PublicAccessType.None,
            IDictionary<string, string>? metadata = null,
            BlobContainerEncryptionScopeOptions? encryptionScopeOptions = null)
        {
            var result = _store.Create(publicAccessType, metadata, encryptionScopeOptions);
            return result.Type switch
            {
                StorageResultType.AlreadyExists => null,
                StorageResultType.Success => Response.FromValue(
                    result.Value,
                    new MemoryResponse(HttpStatusCode.OK)),
                _ => throw new NotImplementedException("Unexpected result type: " + result.Type),
            };
        }

        private Response DeleteResponse(
            BlobRequestConditions? conditions = null)
        {
            var result = _store.Delete(conditions);
            return result switch
            {
                StorageResultType.DoesNotExist => throw new RequestFailedException(new MemoryResponse(HttpStatusCode.NotFound)),
                StorageResultType.Success => new MemoryResponse(HttpStatusCode.OK),
                _ => throw new NotImplementedException("Unexpected result type: " + result),
            };
        }

        private Response<bool> DeleteIfExistsResponse(
            BlobRequestConditions? conditions = null)
        {
            var result = _store.Delete(conditions);
            return result switch
            {
                StorageResultType.DoesNotExist => Response.FromValue(
                    false,
                    new MemoryResponse(HttpStatusCode.NotFound)),
                StorageResultType.Success => Response.FromValue(
                    true,
                    new MemoryResponse(HttpStatusCode.OK)),
                _ => throw new NotImplementedException("Unexpected result type: " + result),
            };
        }

        private Response<bool> ExistsResponse()
        {
            if (_store.Exists())
            {
                return Response.FromValue(
                    true,
                    new MemoryResponse(HttpStatusCode.OK));
            }

            return Response.FromValue(
                false,
                new MemoryResponse(HttpStatusCode.NotFound));
        }
    }
}

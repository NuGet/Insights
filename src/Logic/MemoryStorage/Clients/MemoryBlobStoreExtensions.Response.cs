// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using Azure.Storage;
using Azure.Storage.Blobs.Models;

#nullable enable

namespace NuGet.Insights.MemoryStorage
{
    public static partial class MemoryBlobStoreExtensions
    {
        public static Response<BlobLease> AcquireLeaseResponse(
            this MemoryBlobStore store,
            TimeSpan duration,
            RequestConditions? conditions = null)
        {
            var result = store.AcquireLease(duration, conditions);
            return result.Type switch
            {
                StorageResultType.ContainerDoesNotExist => throw new RequestFailedException(new MemoryResponse(HttpStatusCode.NotFound)),
                StorageResultType.DoesNotExist => throw new RequestFailedException(new MemoryResponse(HttpStatusCode.NotFound)),
                StorageResultType.BlockedByLease => throw new RequestFailedException(new MemoryResponse(HttpStatusCode.Conflict)),
                StorageResultType.Success => Response.FromValue(
                    result.Value,
                    new MemoryResponse(HttpStatusCode.Created)),
                _ => throw new NotImplementedException("Unexpected result type: " + result.Type),
            };
        }

        public static Response<BlobLease> BreakLeaseResponse(
            this MemoryBlobStore store,
            TimeSpan? breakPeriod = null,
            RequestConditions? conditions = null)
        {
            var result = store.BreakLease(breakPeriod, conditions);
            return result.Type switch
            {
                StorageResultType.ContainerDoesNotExist => throw new RequestFailedException(new MemoryResponse(HttpStatusCode.NotFound)),
                StorageResultType.HasNoLease => throw new RequestFailedException(new MemoryResponse(HttpStatusCode.Conflict)),
                StorageResultType.Success => Response.FromValue(
                    result.Value,
                    new MemoryResponse(HttpStatusCode.OK)),
                _ => throw new NotImplementedException("Unexpected result type: " + result.Type),
            };
        }

        public static Response<ReleasedObjectInfo> ReleaseLeaseResponse(
            this MemoryBlobStore store,
            string leaseId,
            RequestConditions? conditions = null)
        {
            var result = store.ReleaseLease(leaseId, conditions);
            return result.Type switch
            {
                StorageResultType.ContainerDoesNotExist => throw new RequestFailedException(new MemoryResponse(HttpStatusCode.NotFound)),
                StorageResultType.BlockedByDifferentLease => throw new RequestFailedException(new MemoryResponse(HttpStatusCode.Conflict)),
                StorageResultType.Success => Response.FromValue(
                    result.Value,
                    new MemoryResponse(HttpStatusCode.OK)),
                _ => throw new NotImplementedException("Unexpected result type: " + result.Type),
            };
        }

        public static Response<BlobLease> RenewLeaseResponse(
            this MemoryBlobStore store,
            string leaseId,
            RequestConditions? conditions = null)
        {
            var result = store.RenewLease(leaseId, conditions);
            return result.Type switch
            {
                StorageResultType.ContainerDoesNotExist => throw new RequestFailedException(new MemoryResponse(HttpStatusCode.NotFound)),
                StorageResultType.BlockedByDifferentLease => throw new RequestFailedException(new MemoryResponse(HttpStatusCode.Conflict)),
                StorageResultType.Success => Response.FromValue(
                    result.Value,
                    new MemoryResponse(HttpStatusCode.OK)),
                _ => throw new NotImplementedException("Unexpected result type: " + result.Type),
            };
        }

        public static Response DeleteResponse(
            this MemoryBlobStore store,
            DeleteSnapshotsOption deleteSnapshotsOption = DeleteSnapshotsOption.None,
            BlobRequestConditions? conditions = null)
        {
            var result = store.Delete(deleteSnapshotsOption, conditions);
            return result switch
            {
                StorageResultType.ContainerDoesNotExist => throw new RequestFailedException(new MemoryResponse(HttpStatusCode.NotFound)),
                StorageResultType.DoesNotExist => throw new RequestFailedException(new MemoryResponse(HttpStatusCode.NotFound)),
                StorageResultType.Success => new MemoryResponse(HttpStatusCode.NoContent),
                _ => throw new NotImplementedException("Unexpected result type: " + result),
            };
        }

        public static Response<BlobProperties> GetPropertiesResponse(
            this MemoryBlobStore store,
            BlobRequestConditions? conditions = null)
        {
            if (conditions is not null)
            {
                throw new NotImplementedException();
            }

            var result = store.GetProperties();
            return result.Type switch
            {
                StorageResultType.ContainerDoesNotExist => throw new RequestFailedException(new MemoryResponse(HttpStatusCode.NotFound)),
                StorageResultType.DoesNotExist => throw new RequestFailedException(new MemoryResponse(HttpStatusCode.NotFound)),
                StorageResultType.Success => Response.FromValue(result.Value, new MemoryResponse(HttpStatusCode.OK)),
                _ => throw new NotImplementedException("Unexpected result type: " + result.Type),
            };
        }

        public static Response<BlobDownloadStreamingResult> DownloadStreamingResponse(
            this MemoryBlobStore store,
            BlobDownloadOptions? options = null)
        {
            var result = store.DownloadStreaming(options, transferOptions: default);
            return result.Type switch
            {
                StorageResultType.ContainerDoesNotExist => throw new RequestFailedException(new MemoryResponse(HttpStatusCode.NotFound)),
                StorageResultType.DoesNotExist => throw new RequestFailedException(new MemoryResponse(HttpStatusCode.NotFound)),
                StorageResultType.Success => Response.FromValue(result.Value, new MemoryResponse(HttpStatusCode.OK)),
                _ => throw new NotImplementedException("Unexpected result type: " + result.Type),
            };
        }

        public static Response DownloadToResponse(
            this MemoryBlobStore store,
            Stream destination,
            BlobRequestConditions? conditions = null,
            StorageTransferOptions transferOptions = default,
            CancellationToken cancellationToken = default)
        {
            var result = store.DownloadTo(destination, new BlobDownloadOptions { Conditions = conditions }, transferOptions);
            return result.Type switch
            {
                StorageResultType.ContainerDoesNotExist => throw new RequestFailedException(new MemoryResponse(HttpStatusCode.NotFound)),
                StorageResultType.DoesNotExist => throw new RequestFailedException(new MemoryResponse(HttpStatusCode.NotFound)),
                StorageResultType.Success => new MemoryResponse(HttpStatusCode.OK),
                _ => throw new NotImplementedException("Unexpected result type: " + result.Type),
            };
        }

        public static Response<BlobDownloadResult> DownloadContentResponse(
            this MemoryBlobStore store,
            BlobRequestConditions? conditions = null)
        {
            return store.DownloadContentResponse(new BlobDownloadOptions
            {
                Conditions = conditions,
            });
        }

        public static Response<BlobDownloadResult> DownloadContentResponse(
            this MemoryBlobStore store,
            BlobDownloadOptions? options = null)
        {
            var result = store.DownloadContent(options, transferOptions: default);
            return result.Type switch
            {
                StorageResultType.ContainerDoesNotExist => throw new RequestFailedException(new MemoryResponse(HttpStatusCode.NotFound)),
                StorageResultType.DoesNotExist => throw new RequestFailedException(new MemoryResponse(HttpStatusCode.NotFound)),
                StorageResultType.Success => Response.FromValue(result.Value, new MemoryResponse(HttpStatusCode.OK)),
                _ => throw new NotImplementedException("Unexpected result type: " + result.Type),
            };
        }

        public static Response<BlobContentInfo> UploadResponse(
            this MemoryBlobStore store,
            Stream content,
            BlobUploadOptions? options)
        {
            var result = store.Upload(content, options);

            return result.Type switch
            {
                StorageResultType.ContainerDoesNotExist => throw new RequestFailedException(new MemoryResponse(HttpStatusCode.NotFound)),
                StorageResultType.Success => Response.FromValue(result.Value, new MemoryResponse(HttpStatusCode.OK)),
                StorageResultType.ETagMismatch => throw new RequestFailedException(new MemoryResponse(HttpStatusCode.PreconditionFailed)),
                StorageResultType.AlreadyExists => throw new RequestFailedException(new MemoryResponse(HttpStatusCode.Conflict)),
                _ => throw new NotImplementedException("Unexpected result type: " + result.Type),
            };
        }

        public static Response<BlobContentInfo> UploadResponse(
            this MemoryBlobStore store,
            Stream content,
            BlobHttpHeaders? httpHeaders = null,
            IDictionary<string, string>? metadata = null,
            BlobRequestConditions? conditions = null,
            IProgress<long>? progressHandler = null,
            AccessTier? accessTier = null,
            StorageTransferOptions transferOptions = default)
        {
            return store.UploadResponse(content, new BlobUploadOptions
            {
                HttpHeaders = httpHeaders,
                Metadata = metadata,
                Conditions = conditions,
                ProgressHandler = progressHandler,
                AccessTier = accessTier,
                TransferOptions = transferOptions,
            });
        }

        public static Response<BlobInfo> SetMetadataResponse(
            this MemoryBlobStore store,
            IDictionary<string, string> metadata,
            BlobRequestConditions? conditions = null)
        {
            var result = store.SetMetadata(metadata, conditions);
            return result.Type switch
            {
                StorageResultType.ContainerDoesNotExist => throw new RequestFailedException(new MemoryResponse(HttpStatusCode.NotFound)),
                StorageResultType.DoesNotExist => throw new RequestFailedException(new MemoryResponse(HttpStatusCode.NotFound)),
                StorageResultType.BlockedByDifferentLease => throw new RequestFailedException(new MemoryResponse(HttpStatusCode.PreconditionFailed)),
                StorageResultType.Success => Response.FromValue(
                    result.Value,
                    new MemoryResponse(HttpStatusCode.OK)),
                _ => throw new NotImplementedException("Unexpected result type: " + result.Type),
            };
        }
    }
}

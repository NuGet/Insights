// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.Security.Cryptography;
using Azure;
using Azure.Storage;
using Azure.Storage.Blobs.Models;

namespace NuGet.Insights.MemoryStorage
{
    public class MemoryBlobStore
    {
        private const string AcceptRanges = "bytes";

        private readonly object _lock = new();
        private readonly TimeProvider _timeProvider;
        private readonly MemoryBlobContainerStore _parent;
        private string _name;

        private string? _leaseId;
        private DateTimeOffset? _leaseExpiration;
        private TimeSpan _leaseDuration;
        private bool _broken;

        private string? _copyId;
        private CopyStatus? _copyStatus;
        private DateTimeOffset _copyCompletedOn;

        private DateTimeOffset _createdOn;
        private DateTimeOffset _lastModified;
        private bool _exists;
        private MemoryStream? _content;
        private BlobHttpHeaders? _httpHeaders;
        private Dictionary<string, string>? _metadata;

        public MemoryBlobStore(TimeProvider timeProvider, MemoryBlobContainerStore parent, string name)
        {
            _timeProvider = timeProvider;
            _parent = parent;
            _name = name;
        }

        private ETag ETag => _lastModified.ToMemoryETag(weak: false);
        private LeaseState LeaseState => this switch
        {
            { _leaseId: null } => LeaseState.Available,
            { _broken: true } => LeaseState.Broken,
            { _leaseExpiration: DateTimeOffset expiration } when expiration > _timeProvider.GetUtcNow() => LeaseState.Leased,
            _ => LeaseState.Expired,
        };
        private LeaseStatus LeaseStatus => LeaseState switch
        {
            LeaseState.Available => LeaseStatus.Unlocked,
            LeaseState.Broken => LeaseStatus.Unlocked,
            LeaseState.Expired => LeaseStatus.Unlocked,
            LeaseState.Leased => LeaseStatus.Locked,
            LeaseState.Breaking => LeaseStatus.Locked,
            _ => throw new NotImplementedException(),
        };

        public virtual StorageResult<BlobItem> GetBlobItem(BlobTraits traits)
        {
            lock (_lock)
            {
                if (traits != BlobTraits.None && traits != BlobTraits.Metadata)
                {
                    throw new NotImplementedException();
                }

                if (!_parent.Exists())
                {
                    return new(StorageResultType.ContainerDoesNotExist);
                }

                if (!_exists)
                {
                    return new(StorageResultType.DoesNotExist);
                }

                return new(StorageResultType.Success, BlobsModelFactory.BlobItem(
                    _name,
                    metadata: traits.HasFlag(BlobTraits.Metadata) ? _metadata!.ToDictionary() : null,
                    properties: BlobsModelFactory.BlobItemProperties(
                        accessTierInferred: default,
                        blobType: BlobType.Block,
                        cacheControl: _httpHeaders!.CacheControl,
                        contentDisposition: _httpHeaders.ContentDisposition,
                        contentEncoding: _httpHeaders.ContentEncoding,
                        contentHash: _httpHeaders.ContentHash,
                        contentLanguage: _httpHeaders.ContentLanguage,
                        contentLength: _content!.Length,
                        contentType: _httpHeaders.ContentType,
                        createdOn: _createdOn,
                        eTag: ETag,
                        lastModified: _lastModified,
                        leaseState: LeaseState,
                        leaseStatus: LeaseStatus)));
            }
        }

        public virtual StorageResult<BlobContentInfo> Upload(Stream content, BlobUploadOptions? options)
        {
            lock (_lock)
            {
                var result = UploadInternal(content, options);
                if (result != StorageResultType.Success)
                {
                    return new(result);
                }

                return new(StorageResultType.Success, BlobsModelFactory.BlobContentInfo(
                    ETag,
                    _lastModified,
                    _httpHeaders!.ContentHash,
                    default,
                    default,
                    default));
            }
        }

        public virtual StorageResultType Delete(
            DeleteSnapshotsOption snapshotsOption,
            BlobRequestConditions? conditions)
        {
            lock (_lock)
            {
                var conditionResult = EvaluateBlobRequestConditions(conditions, mustExist: true);
                if (conditionResult != StorageResultType.Success)
                {
                    return conditionResult;
                }

                if (snapshotsOption != DeleteSnapshotsOption.None)
                {
                    throw new NotImplementedException();
                }

                _exists = false;
                _leaseId = null;
                _copyId = null;
                _copyStatus = null;
                _httpHeaders = null;
                _metadata = null;
                _content = null;

                return StorageResultType.Success;
            }
        }

        public virtual StorageResult<BlobLease> AcquireLease(TimeSpan duration, RequestConditions? conditions)
        {
            lock (_lock)
            {
                if (conditions is not null)
                {
                    throw new NotImplementedException();
                }

                if (duration < TimeSpan.FromSeconds(15) || duration > TimeSpan.FromSeconds(60))
                {
                    throw new NotImplementedException();
                }

                if (!_parent.Exists())
                {
                    return new(StorageResultType.ContainerDoesNotExist);
                }

                if (!_exists)
                {
                    return new(StorageResultType.DoesNotExist);
                }

                if (LeaseState == LeaseState.Leased)
                {
                    return new(StorageResultType.BlockedByLease);
                }

                _leaseId = Guid.NewGuid().ToString();
                _leaseExpiration = _timeProvider.GetUtcNow().Add(duration);
                _leaseDuration = duration;
                _broken = false;
                return new(StorageResultType.Success, MakeBlobLease());
            }
        }

        public virtual StorageResult<BlobLease> RenewLease(string leaseId, RequestConditions? conditions)
        {
            lock (_lock)
            {
                if (conditions is not null)
                {
                    throw new NotImplementedException();
                }

                if (_leaseId != leaseId)
                {
                    return new(StorageResultType.BlockedByDifferentLease);
                }

                _leaseExpiration = _timeProvider.GetUtcNow().Add(_leaseDuration);
                return new(StorageResultType.Success, MakeBlobLease());
            }
        }

        public virtual StorageResult<ReleasedObjectInfo> ReleaseLease(string leaseId, RequestConditions? conditions)
        {
            lock (_lock)
            {
                if (conditions is not null)
                {
                    throw new NotImplementedException();
                }

                if (_leaseId != leaseId)
                {
                    return new(StorageResultType.BlockedByDifferentLease);
                }

                _leaseId = null;
                _leaseExpiration = null;
                _broken = false;
                return new(StorageResultType.Success, new ReleasedObjectInfo(ETag, _lastModified));
            }
        }

        public virtual StorageResult<BlobDownloadResult> DownloadContent(
            BlobDownloadOptions? options,
            StorageTransferOptions transferOptions)
        {
            lock (_lock)
            {
                var result = DownloadBytes(options, transferOptions);
                if (result.Type != StorageResultType.Success)
                {
                    return new(result.Type);
                }

                return new(StorageResultType.Success, BlobsModelFactory.BlobDownloadResult(
                    content: new BinaryData(result.Value),
                    details: MakeBlobDownloadDetails()));
            }
        }

        public virtual StorageResult<BlobDownloadStreamingResult> DownloadStreaming(
            BlobDownloadOptions? options,
            StorageTransferOptions transferOptions)
        {
            lock (_lock)
            {
                var result = DownloadBytes(options, transferOptions);
                if (result.Type != StorageResultType.Success)
                {
                    return new(result.Type);
                }

                var buffer = result.Value;
                var outputStream = new MemoryStream(buffer.Array!, buffer.Offset, buffer.Count, writable: false, publiclyVisible: true);

                return new(StorageResultType.Success, BlobsModelFactory.BlobDownloadStreamingResult(
                    content: outputStream,
                    details: MakeBlobDownloadDetails()));
            }
        }

        private StorageResult<ArraySegment<byte>> DownloadBytes(
            BlobDownloadOptions? options,
            StorageTransferOptions transferOptions)
        {
            var conditionResult = EvaluateBlobRequestConditions(options?.Conditions, mustExist: true);
            if (conditionResult != StorageResultType.Success)
            {
                return new(conditionResult);
            }

            if (transferOptions != default)
            {
                throw new NotImplementedException();
            }

            EvaluateBlobDownloadOptions(options);

            if (!_content!.TryGetBuffer(out var buffer) || buffer.Array is null)
            {
                throw new NotImplementedException();
            }

            return new(StorageResultType.Success, buffer);
        }

        public virtual StorageResult<BlobProperties> GetProperties()
        {
            lock (_lock)
            {
                var conditionResult = EvaluateBlobRequestConditions(conditions: null, mustExist: true);
                if (conditionResult != StorageResultType.Success)
                {
                    return new(conditionResult);
                }

                return new(StorageResultType.Success, BlobsModelFactory.BlobProperties(
                    acceptRanges: AcceptRanges,
                    blobCopyStatus: _copyStatus,
                    blobType: BlobType.Block,
                    cacheControl: _httpHeaders!.CacheControl,
                    contentDisposition: _httpHeaders.ContentDisposition,
                    contentEncoding: _httpHeaders.ContentEncoding,
                    contentHash: _httpHeaders.ContentHash,
                    contentLanguage: _httpHeaders.ContentLanguage,
                    contentLength: _content!.Length,
                    contentType: _httpHeaders.ContentType,
                    copyId: _copyId,
                    copyCompletedOn: _copyCompletedOn,
                    createdOn: _createdOn,
                    eTag: ETag,
                    lastModified: _lastModified,
                    leaseState: LeaseState,
                    leaseStatus: LeaseStatus,
                    metadata: _metadata!.ToDictionary()));
            }
        }

        public virtual StorageResult<BlobInfo> SetMetadata(IDictionary<string, string> metadata, BlobRequestConditions? conditions)
        {
            lock (_lock)
            {
                var conditionResult = EvaluateBlobRequestConditions(conditions, mustExist: true);
                if (conditionResult != StorageResultType.Success)
                {
                    return new(conditionResult);
                }

                SetMetadata(metadata);
                UpdateLastModified();

                return new(StorageResultType.Success, BlobsModelFactory.BlobInfo(ETag, _lastModified));
            }
        }

        public virtual StorageResult<BlobLease> BreakLease(TimeSpan? breakPeriod, RequestConditions? conditions)
        {
            lock (_lock)
            {
                if (breakPeriod != TimeSpan.Zero)
                {
                    throw new NotImplementedException();
                }

                if (conditions is not null)
                {
                    throw new NotImplementedException();
                }

                if (!_parent.Exists())
                {
                    return new(StorageResultType.ContainerDoesNotExist);
                }

                if (!_exists)
                {
                    return new(StorageResultType.DoesNotExist);
                }

                if (_leaseId == null)
                {
                    return new(StorageResultType.HasNoLease);
                }

                var lease = MakeBlobLease();
                _leaseId = null;
                _leaseExpiration = null;
                _broken = true;
                return new(StorageResultType.Success, lease);
            }
        }


        public StorageResultType CopyFrom(
            MemoryBlobStore source,
            string copyId,
            BlobCopyFromUriOptions options)
        {
            if (options.ShouldSealDestination.HasValue)
            {
                throw new NotImplementedException();
            }

            if (options.Tags is not null)
            {
                throw new NotImplementedException();
            }

            if (options.AccessTier.HasValue)
            {
                throw new NotImplementedException();
            }

            if (options.RehydratePriority.HasValue)
            {
                throw new NotImplementedException();
            }

            if (options.DestinationImmutabilityPolicy is not null)
            {
                throw new NotImplementedException();
            }

            if (options.LegalHold.HasValue)
            {
                throw new NotImplementedException();
            }

            if (options.SourceAuthentication is not null)
            {
                throw new NotImplementedException();
            }

            if (options.CopySourceTagsMode.HasValue)
            {
                throw new NotImplementedException();
            }

            var downloadResult = source.DownloadStreaming(
                new BlobDownloadOptions { Conditions = options.SourceConditions },
                transferOptions: default);

            lock (_lock)
            {
                _copyId = copyId;
                _copyStatus = CopyStatus.Failed;

                if (downloadResult.Type != StorageResultType.Success)
                {
                    return downloadResult.Type;
                }

                var uploadResult = UploadInternal(
                    downloadResult.Value.Content,
                    new BlobUploadOptions
                    {
                        Conditions = options.DestinationConditions,
                        Metadata = options.Metadata,
                    });
                if (uploadResult != StorageResultType.Success)
                {
                    return uploadResult;
                }

                _copyStatus = CopyStatus.Success;
                _copyCompletedOn = _timeProvider.GetUtcNow();
                return StorageResultType.Success;
            }
        }

        private StorageResultType UploadInternal(Stream content, BlobUploadOptions? options)
        {
            var conditionResult = EvaluateBlobRequestConditions(options?.Conditions, mustExist: false);
            if (conditionResult != StorageResultType.Success)
            {
                return conditionResult;
            }

            _httpHeaders = new();
            _metadata = new();

            if (options is not null)
            {
                if (options.ProgressHandler is not null)
                {
                    throw new NotImplementedException();
                }

                if (options.AccessTier.HasValue)
                {
                    throw new NotImplementedException();
                }

                if (options.TransferOptions != default)
                {
                    throw new NotImplementedException();
                }

                if (options.ImmutabilityPolicy is not null)
                {
                    throw new NotImplementedException();
                }

                if (options.LegalHold is not null)
                {
                    throw new NotImplementedException();
                }

                if (options.TransferValidation is not null)
                {
                    throw new NotImplementedException();
                }

                if (options.HttpHeaders is not null)
                {
                    SetHttpHeaders(options.HttpHeaders);
                }

                if (options.Metadata is not null)
                {
                    SetMetadata(options.Metadata);
                }
            }

            var newContent = new MemoryStream();
            content.CopyTo(newContent);
            newContent.Position = 0;

            var hash = MD5.HashData(newContent);
            newContent.Position = 0;

            if (options?.HttpHeaders?.ContentHash is not null
                && !hash.SequenceEqual(options.HttpHeaders.ContentHash))
            {
                return StorageResultType.HashMismatch;
            }

            _httpHeaders.ContentHash = hash;

            _content = newContent;
            UpdateLastModified();
            if (!_exists)
            {
                _createdOn = _lastModified;
            }
            _exists = true;

            return StorageResultType.Success;
        }

        private BlobDownloadDetails MakeBlobDownloadDetails()
        {
            return BlobsModelFactory.BlobDownloadDetails(
                blobType: BlobType.Block,
                contentLength: _content!.Length,
                contentType: _httpHeaders!.ContentType,
                contentHash: _httpHeaders.ContentHash,
                lastModified: _lastModified,
                metadata: _metadata!.ToDictionary(),
                contentEncoding: _httpHeaders.ContentEncoding,
                cacheControl: _httpHeaders.CacheControl,
                contentDisposition: _httpHeaders.ContentDisposition,
                contentLanguage: _httpHeaders.ContentLanguage,
                leaseState: LeaseState,
                leaseStatus: LeaseStatus,
                acceptRanges: AcceptRanges,
                createdOn: _createdOn,
                eTag: ETag);
        }

        private static void EvaluateBlobDownloadOptions(BlobDownloadOptions? options)
        {
            if (options is not null)
            {
                if (options.TransferValidation is not null)
                {
                    throw new NotImplementedException();
                }

                if (options.Range != default)
                {
                    throw new NotImplementedException();
                }

                if (options.ProgressHandler is not null)
                {
                    throw new NotImplementedException();
                }

                if (options.TransferValidation is not null)
                {
                    throw new NotImplementedException();
                }
            }
        }

        private void SetHttpHeaders(BlobHttpHeaders headers)
        {
            _httpHeaders!.CacheControl = headers.CacheControl;
            _httpHeaders.ContentDisposition = headers.ContentDisposition;
            _httpHeaders.ContentEncoding = headers.ContentEncoding;
            _httpHeaders.ContentLanguage = headers.ContentLanguage;
            _httpHeaders.ContentType = headers.ContentType;
            _httpHeaders.ContentHash = headers.ContentHash;
        }

        private BlobLease MakeBlobLease()
        {
            return BlobsModelFactory.BlobLease(ETag, _lastModified, _leaseId);
        }

        private StorageResultType EvaluateBlobRequestConditions(BlobRequestConditions? conditions, bool mustExist)
        {
            if (!_parent.Exists())
            {
                return StorageResultType.ContainerDoesNotExist;
            }

            if (mustExist && !_exists)
            {
                return StorageResultType.DoesNotExist;
            }

            if (conditions is not null)
            {
                if (conditions.IfMatch.HasValue)
                {
                    if (conditions.IfMatch != ETag)
                    {
                        return StorageResultType.ETagMismatch;
                    }
                }

                if (conditions.IfNoneMatch.HasValue)
                {
                    if (conditions.IfNoneMatch != ETag.All)
                    {
                        throw new NotImplementedException();
                    }
                    else
                    {
                        if (_exists)
                        {
                            return StorageResultType.AlreadyExists;
                        }
                    }
                }

                if (conditions.IfModifiedSince.HasValue)
                {
                    throw new NotImplementedException();
                }

                if (conditions.IfUnmodifiedSince.HasValue)
                {
                    throw new NotImplementedException();
                }

                if (conditions.TagConditions is not null)
                {
                    throw new NotImplementedException();
                }

                if (LeaseState == LeaseState.Leased)
                {
                    if (conditions.LeaseId is null)
                    {
                        return StorageResultType.BlockedByLease;
                    }
                    else if (conditions.LeaseId != _leaseId)
                    {
                        return StorageResultType.BlockedByDifferentLease;
                    }
                }
            }
            else if (LeaseState == LeaseState.Leased)
            {
                return StorageResultType.BlockedByLease;
            }

            return StorageResultType.Success;
        }

        private void UpdateLastModified()
        {
            _lastModified = _timeProvider.GetUtcNow();
            _broken = false;
        }

        private void SetMetadata(IDictionary<string, string> metadata)
        {
            _metadata!.Clear();
            foreach (var pair in metadata)
            {
                _metadata.Add(pair.Key, pair.Value);
            }
        }
    }
}

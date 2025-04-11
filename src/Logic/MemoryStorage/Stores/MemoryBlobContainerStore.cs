// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using Azure;
using Azure.Storage.Blobs.Models;

namespace NuGet.Insights.MemoryStorage
{
    public class MemoryBlobContainerStore
    {
        private readonly ConcurrentDictionary<string, MemoryBlobStore> _blobs = new();

        private readonly object _lock = new();
        private readonly TimeProvider _timeProvider;
        private string _name;

        private DateTimeOffset _lastModified;
        private bool _exists;

        public MemoryBlobContainerStore(TimeProvider timeProvider, string name)
        {
            _timeProvider = timeProvider;
            _name = name;
        }

        private ETag ETag => _lastModified.ToMemoryETag(weak: false);

        public virtual StorageResult<BlobContainerItem> GetBlobContainerItem()
        {
            lock (_lock)
            {
                if (!_exists)
                {
                    return new(StorageResultType.DoesNotExist);
                }

                return new(StorageResultType.Success, BlobsModelFactory.BlobContainerItem(
                    _name,
                    BlobsModelFactory.BlobContainerProperties(_lastModified, ETag)));
            }
        }

        public virtual MemoryBlobStore GetBlob(string name)
        {
            return _blobs.GetOrAdd(name, x => new MemoryBlobStore(_timeProvider, this, x));
        }

        public virtual bool Exists()
        {
            lock (_lock)
            {
                return _exists;
            }
        }

        public virtual StorageResult<List<BlobItem>> GetBlobItems(
            BlobTraits traits,
            BlobStates states,
            string? prefix)
        {
            lock (_lock)
            {
                if (states != BlobStates.None)
                {
                    throw new NotImplementedException();
                }

                if (!_exists)
                {
                    return new(StorageResultType.DoesNotExist);
                }

                var blobs = _blobs
                    .Values
                    .Select(x => x.GetBlobItem(traits))
                    .Where(x => x.Type switch
                    {
                        StorageResultType.Success => true,
                        StorageResultType.DoesNotExist => false,
                        _ => throw new NotImplementedException("Unexpected result type: " + x.Type),
                    })
                    .Select(x => x.Value)
                    .OrderBy(x => x.Name, StringComparer.Ordinal)
                    .ToList();
                return new(StorageResultType.Success, blobs);
            }
        }

        public virtual StorageResultType Delete(BlobRequestConditions? conditions)
        {
            lock (_lock)
            {
                if (conditions is not null)
                {
                    throw new NotImplementedException();
                }

                if (!_exists)
                {
                    return StorageResultType.DoesNotExist;
                }

                foreach (var blob in _blobs.Values)
                {
                    var breakResult = blob.BreakLease(TimeSpan.Zero, conditions: null);
                    if (breakResult.Type != StorageResultType.DoesNotExist)
                    {
                        var deleteResult = blob.Delete(DeleteSnapshotsOption.None, conditions: null);
                        if (deleteResult != StorageResultType.Success)
                        {
                            throw new InvalidOperationException("Failed to delete blob: " + deleteResult);
                        }
                    }
                }

                _exists = false;
                return StorageResultType.Success;
            }
        }

        public virtual StorageResult<BlobContainerInfo> Create(
            PublicAccessType publicAccessType,
            IDictionary<string, string>? metadata,
            BlobContainerEncryptionScopeOptions? encryptionScopeOptions)
        {
            lock (_lock)
            {
                if (publicAccessType != PublicAccessType.None)
                {
                    throw new NotImplementedException();
                }

                if (metadata is not null)
                {
                    throw new NotImplementedException();
                }

                if (encryptionScopeOptions is not null)
                {
                    throw new NotImplementedException();
                }

                if (_exists)
                {
                    return new(StorageResultType.AlreadyExists);
                }

                _exists = true;
                _lastModified = _timeProvider.GetUtcNow();
                return new(StorageResultType.Success, BlobsModelFactory.BlobContainerInfo(ETag, _lastModified));
            }
        }
    }
}

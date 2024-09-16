// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography;

#nullable enable

namespace NuGet.Insights
{
    public class TempStreamDirectoryLeaseService
    {
        private readonly StorageSemaphoreLeaseService _leaseService;

        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1);
        private readonly Dictionary<string, SharedLease> _leaseNameToLease = new();

        public TempStreamDirectoryLeaseService(StorageSemaphoreLeaseService leaseService)
        {
            _leaseService = leaseService;
        }

        public async Task<IAsyncDisposable?> WaitForLeaseAsync(TempStreamDirectory dir)
        {
            if (!dir.MaxConcurrentWriters.HasValue)
            {
                return NullAsyncDisposable.Instance;
            }

            await _lock.WaitAsync();
            try
            {
                string dirHash;
                using (var sha256 = SHA256.Create())
                {
                    var bytes = Encoding.UTF8.GetBytes(dir.Path.ToLowerInvariant());
                    dirHash = sha256.ComputeHash(bytes).ToTrimmedBase32();
                }

                var name = $"TempStreamDirectory-{dirHash}";

                if (_leaseNameToLease.TryGetValue(name, out var sharedLease))
                {
                    IAsyncDisposable? handle;
                    if (!sharedLease.IsLeaseActive || (handle = await sharedLease.BorrowAsync()) is null)
                    {
                        _leaseNameToLease.Remove(name);
                    }
                    else
                    {
                        return handle;
                    }
                }

                var result = await _leaseService.WaitAsync(name, dir.MaxConcurrentWriters.Value, dir.SemaphoreTimeout);
                if (!result.Acquired)
                {
                    return null;
                }

                sharedLease = new SharedLease(() => TryRemove(name), result);
                _leaseNameToLease.Add(name, sharedLease);
                return await sharedLease.BorrowAsync();
            }
            finally
            {
                _lock.Release();
            }
        }

        private void TryRemove(string name)
        {
            if (_lock.Wait(millisecondsTimeout: 0))
            {
                try
                {
                    _leaseNameToLease.Remove(name);
                }
                finally
                {
                    _lock.Release();
                }
            }
        }

        private class SharedLease
        {
            private readonly SemaphoreSlim _lock = new SemaphoreSlim(1);
            private readonly HashSet<IAsyncDisposable> _handles = new(ReferenceEqualityComparer<IAsyncDisposable>.Instance);
            private readonly Action _onRelease;
            private readonly AutoRenewingStorageLeaseResult _lease;

            public SharedLease(Action onRelease, AutoRenewingStorageLeaseResult lease)
            {
                _onRelease = onRelease;
                _lease = lease;
            }

            public bool Released { get; private set; }
            public bool IsLeaseActive => !Released && _lease.IsLeaseActive;

            public async Task<IAsyncDisposable?> BorrowAsync()
            {
                await _lock.WaitAsync();
                try
                {
                    if (Released)
                    {
                        return null;
                    }

                    var handle = new LocalLeaseHandle(this);
                    _handles.Add(handle);
                    return handle;
                }
                finally
                {
                    _lock.Release();
                }
            }

            private async ValueTask ReleaseAsync(LocalLeaseHandle handle)
            {
                await _lock.WaitAsync();
                try
                {
                    if (Released)
                    {
                        throw new InvalidOperationException("The lease is already released.");
                    }

                    if (!_handles.Remove(handle))
                    {
                        throw new ArgumentException("The provided lease handle is not valid.", nameof(handle));
                    }

                    if (_handles.Count == 0)
                    {
                        Released = true;
                        _onRelease();
                        await _lease.DisposeAsync();
                    }
                }
                finally
                {
                    _lock.Release();
                }
            }

            private class LocalLeaseHandle : IAsyncDisposable
            {
                private readonly SharedLease _sharedLease;

                public LocalLeaseHandle(SharedLease sharedLease)
                {
                    _sharedLease = sharedLease;
                }

                public async ValueTask DisposeAsync()
                {
                    await _sharedLease.ReleaseAsync(this);
                }
            }
        }
    }
}

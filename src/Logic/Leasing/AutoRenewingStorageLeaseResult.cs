// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights
{
    public class AutoRenewingStorageLeaseResult : BaseLeaseResult<StorageLeaseResult>, IAsyncDisposable
    {
        private readonly StorageLeaseService? _service;
        private readonly CancellationTokenSource? _cts;
        private readonly Task? _renewTask;
        private bool _disposed;

        private AutoRenewingStorageLeaseResult(StorageLeaseService? service, StorageLeaseResult? lease, bool acquired, CancellationTokenSource? cts, Task? renewTask)
            : base(lease, lease?.ETag, lease?.Started, acquired)
        {
            _service = service;
            _cts = cts;
            _renewTask = renewTask;
        }

        public bool IsLeaseActive => !_disposed && _renewTask is not null && !_renewTask.IsCompleted;

        public async ValueTask DisposeAsync()
        {
            if (Acquired)
            {
                if (!_disposed)
                {
                    _cts!.Cancel();
                    await _renewTask!;
                    _cts.Dispose();
                    await _service!.ReleaseAsync(Lease!);
                    _disposed = true;
                }
            }
        }

        public static AutoRenewingStorageLeaseResult Leased(StorageLeaseService service, StorageLeaseResult lease, CancellationTokenSource cts, Task renewTask)
        {
            return new AutoRenewingStorageLeaseResult(service, lease, acquired: true, cts, renewTask);
        }

        public static AutoRenewingStorageLeaseResult NotLeased()
        {
            return new AutoRenewingStorageLeaseResult(service: null, lease: null, acquired: false, cts: null, renewTask: null);
        }
    }
}

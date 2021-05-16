using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Insights
{
    public class AutoRenewingStorageLeaseResult : BaseLeaseResult<StorageLeaseResult>, IAsyncDisposable
    {
        private readonly StorageLeaseService _service;
        private readonly CancellationTokenSource _cts;
        private readonly Task _renewTask;

        private AutoRenewingStorageLeaseResult(StorageLeaseService service, StorageLeaseResult lease, bool acquired, CancellationTokenSource cts, Task renewTask)
            : base(lease, acquired)
        {
            _service = service;
            _cts = cts;
            _renewTask = renewTask;
        }

        public async ValueTask DisposeAsync()
        {
            if (Acquired)
            {
                _cts.Cancel();
                await _renewTask;
                _cts.Dispose();
                await _service.ReleaseAsync(Lease);
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

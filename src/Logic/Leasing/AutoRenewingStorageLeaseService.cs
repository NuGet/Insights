using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Insights
{
    public class AutoRenewingStorageLeaseService
    {
        private const int LeaseSeconds = 60;
        private readonly StorageLeaseService _service;

        public AutoRenewingStorageLeaseService(StorageLeaseService service)
        {
            _service = service;
        }

        public async Task InitializeAsync()
        {
            await _service.InitializeAsync();
        }

        public async Task<AutoRenewingStorageLeaseResult> TryAcquireAsync(string name)
        {
            var lease = await _service.TryAcquireAsync(name, TimeSpan.FromSeconds(LeaseSeconds));
            if (!lease.Acquired)
            {
                return AutoRenewingStorageLeaseResult.NotLeased();
            }

            var cts = new CancellationTokenSource();
            var renewTask = RenewAsync(lease, cts.Token);
            return AutoRenewingStorageLeaseResult.Leased(_service, lease, cts, renewTask);
        }

        private async Task RenewAsync(StorageLeaseResult lease, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(LeaseSeconds / 2), token);
                }
                catch (Exception ex) when ((ex is ObjectDisposedException || ex is TaskCanceledException) && token.IsCancellationRequested)
                {
                    return;
                }

                await _service.RenewAsync(lease);
            }
        }
    }
}

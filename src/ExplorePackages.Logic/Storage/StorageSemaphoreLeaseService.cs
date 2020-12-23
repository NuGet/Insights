using System;
using System.Linq;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages
{
    public class StorageSemaphoreLeaseService
    {
        private readonly AutoRenewingStorageLeaseService _leaseService;

        public StorageSemaphoreLeaseService(AutoRenewingStorageLeaseService leaseService)
        {
            _leaseService = leaseService;
        }

        public async Task InitializeAsync()
        {
            await _leaseService.InitializeAsync();
        }

        public async Task<AutoRenewingStorageLeaseResult> WaitAsync(string name, int count)
        {
            AutoRenewingStorageLeaseResult result = null;
            do
            {
                if (result != null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }

                result = await TryAcquireAsync(name, count);
            }
            while (!result.Acquired);

            return result;
        }

        private async Task<AutoRenewingStorageLeaseResult> TryAcquireAsync(string name, int count)
        {
            var leaseNames = Enumerable
                .Range(0, count)
                .Select(i => $"{name}-semaphore-{i}")
                .OrderBy(x => ThreadLocalRandom.Next())
                .ToList();

            foreach (var leaseName in leaseNames)
            {
                var result = await _leaseService.TryAcquireAsync(leaseName);
                if (result.Acquired)
                {
                    return result;
                }
            }

            return AutoRenewingStorageLeaseResult.NotLeased();
        }
    }
}

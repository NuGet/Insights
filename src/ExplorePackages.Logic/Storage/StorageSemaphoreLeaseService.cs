using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages
{
    public class StorageSemaphoreLeaseService
    {
        private readonly AutoRenewingStorageLeaseService _leaseService;
        private readonly ILogger<StorageSemaphoreLeaseService> _logger;

        public StorageSemaphoreLeaseService(AutoRenewingStorageLeaseService leaseService, ILogger<StorageSemaphoreLeaseService> logger)
        {
            _leaseService = leaseService;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await _leaseService.InitializeAsync();
        }

        public async Task<AutoRenewingStorageLeaseResult> WaitAsync(string name, int count)
        {
            int attempt = 0;
            AutoRenewingStorageLeaseResult result = null;
            do
            {
                attempt++;
                if (result != null)
                {
                    var sleepDuration = TimeSpan.FromMilliseconds(Math.Max(attempt * 500, 10_000));
                    _logger.LogWarning("Storage semaphore {Name} of count {Count} is not available. Trying again in {SleepDuration}.", name, count, sleepDuration);
                    await Task.Delay(sleepDuration);
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

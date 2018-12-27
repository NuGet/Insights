using System;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic
{
    public class SingletonService : ISingletonService
    {
        private const string LeaseName = "Knapcode.ExplorePackages";
        private static readonly TimeSpan Duration = TimeSpan.FromHours(1);

        private static SemaphoreSlim _lock = new SemaphoreSlim(1);
        private readonly ILeaseService _leaseService;
        private readonly ILogger<SingletonService> _logger;
        private LeaseEntity _lease;

        public SingletonService(
            ILeaseService leaseService,
            ILogger<SingletonService> logger)
        {
            _leaseService = leaseService;
            _logger = logger;
        }

        public async Task AcquireOrRenewAsync()
        {
            await AcquireOrRenewAsync(acquire: true);
        }

        public async Task RenewAsync()
        {
            await AcquireOrRenewAsync(acquire: false);
        }

        private async Task AcquireOrRenewAsync(bool acquire)
        {
            var lockAcquired = false;
            try
            {
                await _lock.WaitAsync();
                lockAcquired = true;

                if (_lease == null)
                {
                    if (acquire)
                    {
                        _lease = await _leaseService.AcquireAsync(LeaseName, Duration);
                    }
                    else
                    {
                        throw new InvalidOperationException("The provided lease was not acquired in the first place.");
                    }
                }
                else
                {
                    await _leaseService.RenewAsync(_lease, Duration);
                }
            }
            finally
            {
                if (lockAcquired)
                {
                    _lock.Release();
                }
            }
        }

        public async Task ReleaseAsync()
        {
            var acquired = false;
            try
            {
                await _lock.WaitAsync();
                acquired = true;

                if (_lease == null)
                {
                    return;
                }
                else
                {
                    var released = await _leaseService.TryReleaseAsync(_lease);
                    _lease = null;

                    if (released)
                    {
                        _logger.LogInformation("The singleton lease was released.");
                    }
                    else
                    {
                        _logger.LogWarning("The singleton lease was acquired by someone else and therefore couldn't be released.");
                    }
                }
            }
            finally
            {
                if (acquired)
                {
                    _lock.Release();
                }
            }
        }
    }
}

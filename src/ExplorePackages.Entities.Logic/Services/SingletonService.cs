using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Entities
{
    public class SingletonService : ISingletonService
    {
        public const string LeaseName = "Knapcode.ExplorePackages";
        private static readonly TimeSpan Duration = TimeSpan.FromHours(1);

        private static SemaphoreSlim _lock = new SemaphoreSlim(1);
        private readonly IDatabaseLeaseService _leaseService;
        private readonly ILogger<SingletonService> _logger;
        private LeaseEntity _lease;

        public SingletonService(
            IDatabaseLeaseService leaseService,
            ILogger<SingletonService> logger)
        {
            _leaseService = leaseService;
            _logger = logger;
        }

        public async Task BreakAsync()
        {
            var lockAcquired = false;
            try
            {
                await _lock.WaitAsync();
                lockAcquired = true;

                await _leaseService.BreakAsync(LeaseName);
                _lease = null;
                _logger.LogInformation("The singleton lease is now available.");
            }
            finally
            {
                if (lockAcquired)
                {
                    _lock.Release();
                }
            }
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
                        var result = await _leaseService.TryAcquireAsync(LeaseName, Duration);
                        if (result.Acquired)
                        {
                            _lease = result.Lease;
                        }
                        else
                        {
                            throw new InvalidOperationException("The singleton lease was acquired by someone else and therefore couldn't be acquired.");
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException("The singleton lease was not acquired in the first place.");
                    }
                }
                else
                {
                    if (!await _leaseService.TryRenewAsync(_lease, Duration))
                    {
                        _lease = null;
                        throw new InvalidOperationException("The singleton lease was acquired by someone else and therefore couldn't be renewed.");
                    }
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

        public async Task ReleaseInAsync(TimeSpan duration)
        {
            const string message = "The singleton lease was acquired by someone else and therefore couldn't be released.";

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
                    if (duration <= TimeSpan.Zero)
                    {
                        if (await _leaseService.TryReleaseAsync(_lease))
                        {
                            _logger.LogInformation("The singleton lease was released.");
                        }
                        else
                        {
                            _logger.LogWarning(message);
                        }
                    }
                    else
                    {
                        if (await _leaseService.TryRenewAsync(_lease, duration))
                        {
                            _logger.LogInformation("The singleton lease will be released in {Duration}.", duration);
                        }
                        else
                        {
                            _logger.LogWarning(message);
                        }
                    }

                    _lease = null;
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

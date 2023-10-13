// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Insights
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

        public async Task<AutoRenewingStorageLeaseResult> WaitAsync(string name, int count, TimeSpan timeout)
        {
            var attempt = 0;
            var sw = Stopwatch.StartNew();
            AutoRenewingStorageLeaseResult result = null;
            do
            {
                if (result != null)
                {
                    var sleepDuration = TimeSpan.FromSeconds(Math.Max(attempt, 10));
                    _logger.LogTransientWarning("Storage semaphore {Name} of count {Count} is not available. Trying again in {SleepDurationMs}ms.", name, count, sleepDuration.TotalMilliseconds);
                    await Task.Delay(sleepDuration);
                }

                attempt++;
                result = await TryAcquireAsync(name, count);
            }
            while (!result.Acquired && (timeout == Timeout.InfiniteTimeSpan || sw.Elapsed < timeout));

            if (result.Acquired)
            {
                _logger.LogInformation("Acquired semaphore {Name} after {DurationMs}ms.", name, sw.Elapsed.TotalMilliseconds);
            }
            else
            {
                _logger.LogTransientWarning("Failed to acquire semaphore {Name} after {DurationMs}ms.", name, sw.Elapsed.TotalMilliseconds);
            }

            return result;
        }

        private async Task<AutoRenewingStorageLeaseResult> TryAcquireAsync(string name, int count)
        {
            var leaseNames = Enumerable
                .Range(0, count)
                .Select(i => $"{name}-Semaphore-{i}")
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

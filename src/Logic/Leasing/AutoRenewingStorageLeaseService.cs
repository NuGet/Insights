// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Insights
{
    public class AutoRenewingStorageLeaseService
    {
        private const int LeaseSeconds = 60;
        private readonly StorageLeaseService _service;
        private readonly ILogger<AutoRenewingStorageLeaseService> _logger;

        public AutoRenewingStorageLeaseService(StorageLeaseService service, ILogger<AutoRenewingStorageLeaseService> logger)
        {
            _service = service;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await _service.InitializeAsync();
        }

        public async Task<AutoRenewingStorageLeaseResult> TryAcquireWithRetryAsync(string name)
        {
            return await TryAcquireWithRetryAsync(name, maxAttempts: 3);
        }

        public async Task<AutoRenewingStorageLeaseResult> TryAcquireWithRetryAsync(string name, int maxAttempts)
        {
            var attempt = 0;
            var sw = Stopwatch.StartNew();
            while (true)
            {
                if (attempt > 0)
                {
                    var sleepDuration = TimeSpan.FromSeconds(Math.Max(attempt, 10));
                    _logger.LogTransientWarning("After attempt {Attempt}, storage lease {Name} is not available. Trying again in {SleepDurationMs}ms.", attempt, name, sleepDuration.TotalMilliseconds);
                    await Task.Delay(sleepDuration);
                }

                attempt++;
                var result = await TryAcquireAsync(name);
                if (result.Acquired)
                {
                    _logger.LogInformation("Acquired storage lease {Name} after {DurationMs}ms and {Attempt} attempts.", name, sw.Elapsed.TotalMilliseconds, attempt);
                    return result;
                }
                else if (attempt >= maxAttempts)
                {
                    _logger.LogTransientWarning("Failed to acquire storage lease {Name} after {DurationMs}ms and {Attempt} attempts.", name, sw.Elapsed.TotalMilliseconds, attempt);
                    return result;
                }
            }
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

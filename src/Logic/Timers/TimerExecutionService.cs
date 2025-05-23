// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class TimerExecutionService
    {
        public static readonly string PartitionKey = string.Empty;

        private readonly IReadOnlyDictionary<string, ITimer> _nameToTimer;
        private readonly AutoRenewingStorageLeaseService _leaseService;
        private readonly SpecificTimerExecutionService _timerExecutionService;
        private readonly ILogger<TimerExecutionService> _logger;

        public TimerExecutionService(
            IEnumerable<ITimer> timers,
            AutoRenewingStorageLeaseService leaseService,
            SpecificTimerExecutionService timerExecutionService,
            ILogger<TimerExecutionService> logger)
        {
            var timerList = timers.ToList();
            var duplicateNames = timerList
                .GroupBy(x => x.Name)
                .Where(g => g.Count() > 1)
                .Select(g => $"'{g.Key}' ({g.Count()})")
                .ToList();
            if (duplicateNames.Any())
            {
                throw new ArgumentException("There are timers with duplicate names: " + string.Join(", ", duplicateNames));
            }

            _nameToTimer = timerList.ToDictionary(x => x.Name);
            _leaseService = leaseService;
            _timerExecutionService = timerExecutionService;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await Task.WhenAll(
                _leaseService.InitializeAsync(),
                _timerExecutionService.InitializeAsync());
        }

        public async Task<IReadOnlyList<TimerState>> GetStateAsync()
        {
            return await _timerExecutionService.GetStateAsync(_nameToTimer.Values);
        }

        public async Task<TimerState> GetStateAsync(string timerName)
        {
            var timer = ValidateAndGetTimer(timerName);
            var states = await _timerExecutionService.GetStateAsync([timer]);
            return states.First(x => x.Name == timerName);
        }

        public async Task SetIsEnabledAsync(string timerName, bool isEnabled)
        {
            var timer = ValidateAndGetTimer(timerName);
            await _timerExecutionService.SetIsEnabledAsync(timer, isEnabled);
        }

        public async Task SetNextRunAsync(string timerName, DateTimeOffset nextRun)
        {
            var timer = ValidateAndGetTimer(timerName);
            var state = await GetStateAsync(timerName);
            await _timerExecutionService.SetNextRunAsync(timer, state.IsEnabledInStorage, nextRun);
        }

        public async Task AbortAsync(string timerName)
        {
            var timer = ValidateAndGetTimer(timerName);
            await timer.AbortAsync();
        }

        private ITimer ValidateAndGetTimer(string timerName)
        {
            if (!_nameToTimer.TryGetValue(timerName, out var timer))
            {
                throw new ArgumentException("The provided timer name is not recognized.", nameof(timerName));
            }

            return timer;
        }

        public async Task<bool> ExecuteNowAsync(string timerName)
        {
            var timers = new[] { ValidateAndGetTimer(timerName) };
            return await _timerExecutionService.ExecuteAsync(timers, executeNow: true);
        }

        public async Task DestroyOutputAsync(string timerName)
        {
            var timer = ValidateAndGetTimer(timerName);
            await timer.DestroyAsync();
        }

        public async Task<bool> ExecuteAsync()
        {
            await using (var lease = await _leaseService.TryAcquireWithRetryAsync("TimerExecutionService"))
            {
                if (!lease.Acquired)
                {
                    _logger.LogInformation("Another thread is executing this method.");
                    return false;
                }

                await _timerExecutionService.ExecuteAsync(_nameToTimer.Values, executeNow: false);
                return true;
            }
        }
    }
}

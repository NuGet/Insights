// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuGet.Insights
{
    public class TimerExecutionService
    {
        public static readonly string PartitionKey = string.Empty;

        private readonly IReadOnlyDictionary<string, ITimer> _nameToTimer;
        private readonly SpecificTimerExecutionService _timerExecutionService;

        public TimerExecutionService(
            IEnumerable<ITimer> timers,
            SpecificTimerExecutionService timerExecutionService)
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
            _timerExecutionService = timerExecutionService;
        }

        public async Task InitializeAsync()
        {
            await _timerExecutionService.InitializeAsync(_nameToTimer.Values);
        }

        public async Task<IReadOnlyList<TimerState>> GetStateAsync()
        {
            return await _timerExecutionService.GetStateAsync(_nameToTimer.Values);
        }

        public async Task SetIsEnabledAsync(string timerName, bool isEnabled)
        {
            var timer = ValidateAndGetTimer(timerName);
            await _timerExecutionService.SetIsEnabledAsync(timer, isEnabled);
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

        public async Task ExecuteAsync()
        {
            await _timerExecutionService.ExecuteAsync(_nameToTimer.Values, executeNow: false);
        }
    }
}

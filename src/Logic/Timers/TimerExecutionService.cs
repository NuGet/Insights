// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NuGet.Insights
{
    public class TimerExecutionService
    {
        public static readonly string PartitionKey = string.Empty;

        private readonly IReadOnlyDictionary<string, ITimer> _nameToTimer;
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly AutoRenewingStorageLeaseService _leaseService;
        private readonly IOptions<NuGetInsightsSettings> _options;
        private readonly ITelemetryClient _telemetryClient;
        private readonly ILogger<TimerExecutionService> _logger;

        public TimerExecutionService(
            ServiceClientFactory serviceClientFactory,
            IEnumerable<ITimer> timers,
            AutoRenewingStorageLeaseService leaseService,
            IOptions<NuGetInsightsSettings> options,
            ITelemetryClient telemetryClient,
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
            _serviceClientFactory = serviceClientFactory;
            _leaseService = leaseService;
            _options = options;
            _telemetryClient = telemetryClient;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await _leaseService.InitializeAsync();
            await (await GetTableAsync()).CreateIfNotExistsAsync(retry: true);
            foreach (var timer in _nameToTimer.Values)
            {
                await timer.InitializeAsync();
            }
        }

        public async Task<IReadOnlyList<TimerState>> GetStateAsync()
        {
            var pairs = _nameToTimer
                .OrderBy(x => x.Value.Order)
                .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var isRunningTask = Task.WhenAll(pairs.Select(x => x.Value.IsRunningAsync()));
            var table = await GetTableAsync();
            var entitiesTask = table
                .QueryAsync<TimerEntity>(e => e.PartitionKey == PartitionKey)
                .ToListAsync(_telemetryClient.StartQueryLoopMetrics());

            await Task.WhenAll(isRunningTask, entitiesTask);

            var nameToEntity = (await entitiesTask).ToDictionary(x => x.RowKey);

            return pairs
                .Zip(await isRunningTask, (pair, isRunning) =>
                {
                    nameToEntity.TryGetValue(pair.Key, out var entity);

                    return new TimerState
                    {
                        Name = pair.Key,
                        IsRunning = isRunning,
                        IsEnabledInConfig = pair.Value.IsEnabled,
                        IsEnabledInStorage = entity?.IsEnabled ?? pair.Value.AutoStart,
                        LastExecuted = entity?.LastExecuted,
                        Frequency = pair.Value.Frequency,
                    };
                })
                .ToList();
        }

        public async Task SetIsEnabled(string timerName, bool isEnabled)
        {
            ValidateAndGetTimer(timerName);
            var table = await GetTableAsync();
            var entity = new TimerEntity(timerName) { IsEnabled = isEnabled };
            await table.UpsertEntityAsync(entity);
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
            return await ExecuteAsync(new HashSet<string> { timerName }, executeNow: true);
        }

        public async Task ExecuteAsync()
        {
            await using (var lease = await _leaseService.TryAcquireAsync("TimerExecutionService"))
            {
                if (!lease.Acquired)
                {
                    _logger.LogInformation("Another thread is executing this method.");
                    return;
                }

                await ExecuteAsync(timerNames: null, executeNow: false);
            }
        }

        private async Task<bool> ExecuteAsync(ISet<string> timerNames, bool executeNow)
        {
            if (timerNames != null)
            {
                // Validate the provided timer names.
                foreach (var timerName in timerNames)
                {
                    ValidateAndGetTimer(timerName);
                }
            }

            // Get the existing timer entities.
            var table = await GetTableAsync();
            var entities = await table.QueryAsync<TimerEntity>(x => x.PartitionKey == PartitionKey).ToListAsync(_telemetryClient.StartQueryLoopMetrics());
            var nameToEntity = entities.ToDictionary(x => x.RowKey);

            // Determine what to do for each timer.
            var toExecute = new List<(ITimer timer, TimerEntity entity, Func<Task> persistAsync)>();
            var now = DateTimeOffset.UtcNow;
            foreach (var timer in _nameToTimer.Values)
            {
                if (timerNames != null && !timerNames.Contains(timer.Name))
                {
                    continue;
                }

                if (!timer.IsEnabled)
                {
                    _logger.LogInformation("Timer {Name} will not be run because it is disabled in config.", timer.Name);
                }
                else if (!nameToEntity.TryGetValue(timer.Name, out var entity))
                {
                    entity = new TimerEntity(timer.Name) { IsEnabled = timer.AutoStart };

                    if (executeNow || entity.IsEnabled)
                    {
                        toExecute.Add((
                            timer,
                            entity,
                            () => table.AddEntityAsync(entity)));
                        _logger.LogInformation("Timer {Name} will be run for the first time.", timer.Name);
                    }
                    else
                    {
                        _logger.LogInformation("Timer {Name} will be initialized without running.", timer.Name);
                        await table.AddEntityAsync(entity);
                    }
                }
                else if (executeNow)
                {
                    _logger.LogInformation("Timer {Name} will be run because it being run on demand.", timer.Name);
                    toExecute.Add((
                        timer,
                        entity,
                        () => table.UpdateEntityAsync(entity, entity.ETag, mode: TableUpdateMode.Replace)));
                }
                else if (!entity.IsEnabled)
                {
                    _logger.LogInformation("Timer {Name} will not be run because it is disabled in storage.", timer.Name);
                }
                else if (!entity.LastExecuted.HasValue)
                {
                    _logger.LogInformation("Timer {Name} will be run because it has never been run before.", timer.Name);
                    toExecute.Add((
                        timer,
                        entity,
                        () => table.UpdateEntityAsync(entity, entity.ETag, mode: TableUpdateMode.Replace)));
                }
                else if ((now - entity.LastExecuted.Value) < timer.Frequency)
                {
                    _logger.LogInformation("Timer {Name} will not be run because it has been executed too recently.", timer.Name);
                }
                else
                {
                    _logger.LogInformation("Timer {Name} will be run because it has hasn't been run recently enough.", timer.Name);
                    toExecute.Add((
                        timer,
                        entity,
                        () => table.UpdateEntityAsync(entity, entity.ETag, mode: TableUpdateMode.Replace)));
                }
            }

            // Execute timers in ordered groups.
            var anyExecuted = false;
            foreach (var group in toExecute.GroupBy(x => x.timer.Order).OrderBy(x => x.Key))
            {
                var executed = await Task.WhenAll(group.Select(x => ExecuteAsync(x.timer, x.entity, x.persistAsync, now)));
                anyExecuted |= executed.Any(x => x);
            }

            return anyExecuted;
        }

        private async Task<bool> ExecuteAsync(ITimer timer, TimerEntity entity, Func<Task> persistAsync, DateTimeOffset now)
        {
            bool executed;
            try
            {
                executed = await timer.ExecuteAsync();
                if (executed)
                {
                    _logger.LogInformation("Timer {Name} was executed successfully.", timer.Name);
                }
                else
                {
                    _logger.LogInformation("Timer {Name} was unable to execute.", timer.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Timer {Name} failed with an exception.", timer.Name);
                executed = true; // If a timer fails, still update the timestamp to avoid repeated errors.
            }

            if (executed)
            {
                entity.LastExecuted = now;

                // Update table storage after the execute. In other words, if Table Storage fails, we could run the
                // timer again too frequently.
                await persistAsync();
            }

            return executed;
        }

        private async Task<TableClient> GetTableAsync()
        {
            return (await _serviceClientFactory.GetTableServiceClientAsync())
                .GetTableClient(_options.Value.TimerTableName);
        }
    }
}

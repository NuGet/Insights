// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Data.Tables;
using NuGet.Insights.StorageNoOpRetry;

namespace NuGet.Insights
{
    public class SpecificTimerExecutionService
    {
        public static readonly string PartitionKey = string.Empty;

        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IComparer<ITimer> _timerComparer;
        private readonly IOptions<NuGetInsightsSettings> _options;
        private readonly ITelemetryClient _telemetryClient;
        private readonly ILogger<SpecificTimerExecutionService> _logger;

        public SpecificTimerExecutionService(
            ServiceClientFactory serviceClientFactory,
            IComparer<ITimer> timerComparer,
            IOptions<NuGetInsightsSettings> options,
            ITelemetryClient telemetryClient,
            ILogger<SpecificTimerExecutionService> logger)
        {
            _serviceClientFactory = serviceClientFactory;
            _timerComparer = timerComparer;
            _options = options;
            _telemetryClient = telemetryClient;
            _logger = logger;
        }

        public async Task InitializeAsync(IEnumerable<ITimer> timers)
        {
            await (await GetTableAsync()).CreateIfNotExistsAsync(retry: true);
            foreach (var timer in timers)
            {
                await timer.InitializeAsync();
            }
        }

        public async Task SetIsEnabledAsync(ITimer timer, bool isEnabled)
        {
            var table = await GetTableAsync();
            var entity = new TimerEntity(timer.Name) { IsEnabled = isEnabled };
            await table.UpsertEntityAsync(entity);
        }

        public async Task<IReadOnlyList<TimerState>> GetStateAsync(IEnumerable<ITimer> timers)
        {
            var pairs = timers
                .Order(_timerComparer)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var isRunningTask = Task.WhenAll(pairs.Select(x => x.IsRunningAsync()));
            var table = await GetTableAsync();
            var entitiesTask = table
                .QueryAsync<TimerEntity>(e => e.PartitionKey == PartitionKey)
                .ToListAsync(_telemetryClient.StartQueryLoopMetrics());

            await Task.WhenAll(isRunningTask, entitiesTask);

            var nameToEntity = (await entitiesTask).ToDictionary(x => x.RowKey);

            return pairs
                .Zip(await isRunningTask, (pair, isRunning) =>
                {
                    nameToEntity.TryGetValue(pair.Name, out var entity);

                    return new TimerState
                    {
                        Name = pair.Name,
                        IsRunning = isRunning,
                        IsEnabledInConfig = pair.IsEnabled,
                        IsEnabledInStorage = entity?.IsEnabled ?? pair.AutoStart,
                        LastExecuted = entity?.LastExecuted,
                        Frequency = pair.Frequency,
                        CanAbort = pair.CanAbort,
                        CanDestroy = pair.CanDestroy,
                    };
                })
                .ToList();
        }

        public async Task<bool> ExecuteAsync(IEnumerable<ITimer> timers, bool executeNow)
        {
            // Get the existing timer entities.
            var table = await GetTableAsync();
            var entities = await table
                .QueryAsync<TimerEntity>(x => x.PartitionKey == PartitionKey)
                .ToListAsync(_telemetryClient.StartQueryLoopMetrics());
            var nameToEntity = entities.ToDictionary(x => x.RowKey);

            // Determine what to do for each timer.
            var toExecute = new List<(ITimer timer, TimerEntity entity, Func<Task> persistAsync)>();
            var now = DateTimeOffset.UtcNow;
            foreach (var timer in timers)
            {
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
            var allExecuted = true;
            foreach (var group in GroupAndOrder(toExecute, x => x.timer, _timerComparer))
            {
                var executed = await Task.WhenAll(group.Select(x => ExecuteAsync(x.timer, x.entity, x.persistAsync, now)));
                allExecuted &= executed.All(x => x);
            }

            return allExecuted;
        }

        public static IEnumerable<List<T>> GroupAndOrder<T>(IEnumerable<T> items, Func<T, ITimer> getTimer, IComparer<ITimer> comparer)
        {
            List<T> currentGroup = null; 

            foreach (var timer in items.OrderBy(getTimer, comparer))
            {
                if (currentGroup is not null && comparer.Compare(getTimer(currentGroup[0]), getTimer(timer)) == 0)
                {
                    currentGroup.Add(timer);
                }
                else
                {
                    if (currentGroup is not null)
                    {
                        yield return currentGroup;
                    }

                    currentGroup = [timer];
                }
            }

            if (currentGroup is not null)
            {
                yield return currentGroup;
            }
        }

        private async Task<bool> ExecuteAsync(ITimer timer, TimerEntity entity, Func<Task> persistAsync, DateTimeOffset now)
        {
            var executed = false;
            var error = false;
            try
            {
                _telemetryClient.TrackMetric(
                    "Timer.Execute",
                    1,
                    new Dictionary<string, string> { { "Name", entity.Name } });

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
                error = true; // If a timer fails, still update the timestamp to avoid repeated errors.
            }

            if (executed || error)
            {
                entity.LastExecuted = now;

                // Update table storage after the execute. In other words, if Table Storage fails, we could run the
                // timer again too frequently.
                await persistAsync();
            }

            return executed;
        }

        private async Task<TableClientWithRetryContext> GetTableAsync()
        {
            return (await _serviceClientFactory.GetTableServiceClientAsync())
                .GetTableClient(_options.Value.TimerTableName);
        }
    }
}

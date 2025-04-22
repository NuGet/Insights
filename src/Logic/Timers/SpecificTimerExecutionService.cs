// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Data.Tables;
using NuGet.Insights.StorageNoOpRetry;

namespace NuGet.Insights
{
    public class SpecificTimerExecutionService
    {
        public static readonly string PartitionKey = string.Empty;

        private readonly ContainerInitializationState _initializationState;
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IComparer<ITimer> _timerComparer;
        private readonly TimeProvider _timeProvider;
        private readonly IOptions<NuGetInsightsSettings> _options;
        private readonly ITelemetryClient _telemetryClient;
        private readonly ILogger<SpecificTimerExecutionService> _logger;

        public SpecificTimerExecutionService(
            ServiceClientFactory serviceClientFactory,
            IComparer<ITimer> timerComparer,
            TimeProvider timeProvider,
            IOptions<NuGetInsightsSettings> options,
            ITelemetryClient telemetryClient,
            ILogger<SpecificTimerExecutionService> logger)
        {
            _initializationState = ContainerInitializationState.Table(serviceClientFactory, options.Value.TimerTableName);
            _serviceClientFactory = serviceClientFactory;
            _timerComparer = timerComparer;
            _timeProvider = timeProvider;
            _options = options;
            _telemetryClient = telemetryClient;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await _initializationState.InitializeAsync();
        }

        public async Task SetIsEnabledAsync(ITimer timer, bool isEnabled)
        {
            var table = await GetTableAsync();
            var entity = new TimerEntity(timer.Name) { IsEnabled = isEnabled };
            await table.UpsertEntityAsync(entity);
        }

        public async Task SetNextRunAsync(ITimer timer, bool isEnabled, DateTimeOffset nextRun)
        {
            var table = await GetTableAsync();
            var entity = new TimerEntity(timer.Name) { IsEnabled = isEnabled, NextRun = nextRun };
            await table.UpsertEntityAsync(entity);
        }

        public async Task<IReadOnlyList<TimerState>> GetStateAsync(IEnumerable<ITimer> timers)
        {
            var sortedTimers = timers
                .Order(_timerComparer)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var isRunningTask = Task.WhenAll(sortedTimers.Select(async x =>
            {
                await x.InitializeAsync();
                return await x.IsRunningAsync();
            }));

            var table = await GetTableAsync();
            var entitiesTask = table
                .QueryAsync<TimerEntity>(e => e.PartitionKey == PartitionKey)
                .ToListAsync(_telemetryClient.StartQueryLoopMetrics());

            await Task.WhenAll(isRunningTask, entitiesTask);

            var nameToEntity = (await entitiesTask).ToDictionary(x => x.RowKey);

            return sortedTimers
                .Zip(await isRunningTask, (timer, isRunning) =>
                {
                    nameToEntity.TryGetValue(timer.Name, out var entity);

                    return new TimerState
                    {
                        Name = timer.Name,
                        Title = timer.Title,
                        IsRunning = isRunning,
                        IsEnabledInConfig = timer.IsEnabled,
                        IsEnabledInStorage = entity?.IsEnabled ?? timer.AutoStart,
                        LastExecuted = entity?.LastExecuted,
                        Frequency = timer.Frequency,
                        CanAbort = timer.CanAbort,
                        CanDestroy = timer.CanDestroy,
                        NextRun = entity?.NextRun,
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
            var now = _timeProvider.GetUtcNow();
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
                else if (entity.NextRun.HasValue)
                {
                    if (entity.NextRun <= now)
                    {
                        _logger.LogInformation("Timer {Name} will be run because the next run timestamp is in the past.", timer.Name);
                        toExecute.Add((
                            timer,
                            entity,
                            () => table.UpdateEntityAsync(entity, entity.ETag, mode: TableUpdateMode.Replace)));
                    }
                    else
                    {
                        _logger.LogInformation("Timer {Name} will not be run because the next run timestamp is in the future.", timer.Name);
                    }
                }
                else if (!entity.LastExecuted.HasValue)
                {
                    _logger.LogInformation("Timer {Name} will be run because it has never been run before.", timer.Name);
                    toExecute.Add((
                        timer,
                        entity,
                        () => table.UpdateEntityAsync(entity, entity.ETag, mode: TableUpdateMode.Replace)));
                }
                else
                {
                    bool execute = false;
                    TimeSpan scheduleOffset;
                    if (timer.Frequency.Schedule is not null)
                    {
                        var nextRun = timer.Frequency.Schedule.GetNextOccurrence(DateTime.SpecifyKind(entity.LastExecuted.Value.UtcDateTime, DateTimeKind.Utc));
                        scheduleOffset = now - nextRun;
                        if (scheduleOffset >= TimeSpan.Zero)
                        {
                            _logger.LogInformation("Timer {Name} will run because it has hasn't been run recently enough (based on configured schedule {Schedule}).", timer.Name, timer.Frequency);
                            execute = true;
                        }
                        else
                        {
                            _logger.LogInformation("Timer {Name} will not run because it has been executed too recently (based on configured schedule {Schedule}).", timer.Name, timer.Frequency);
                        }
                    }
                    else
                    {
                        scheduleOffset = now - (entity.LastExecuted.Value + timer.Frequency.TimeSpan.Value);
                        if (scheduleOffset >= TimeSpan.Zero)
                        {
                            _logger.LogInformation("Timer {Name} will run because it has hasn't been run recently enough (based on configured frequency {Frequency}).", timer.Name, timer.Frequency);
                            execute = true;
                        }
                        else
                        {
                            _logger.LogInformation("Timer {Name} will not run because it has been executed too recently (based on configured frequency {Frequency}).", timer.Name, timer.Frequency);
                        }
                    }

                    if (execute)
                    {
                        _telemetryClient.TrackMetric(
                            "Timer.ScheduleOffsetSeconds",
                            scheduleOffset.TotalSeconds,
                            new Dictionary<string, string> { { "Name", entity.Name } });

                        toExecute.Add((
                            timer,
                            entity,
                            () => table.UpdateEntityAsync(entity, entity.ETag, mode: TableUpdateMode.Replace)));
                    }
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
                    MetricNames.TimerExecute,
                    1,
                    new Dictionary<string, string> { { "Name", entity.Name } });

                await timer.InitializeAsync();
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
                entity.NextRun = null;

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

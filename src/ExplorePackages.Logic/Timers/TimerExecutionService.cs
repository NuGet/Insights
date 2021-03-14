using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.Timers
{
    public class TimerExecutionService
    {
        public static readonly string PartitionKey = string.Empty;

        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IEnumerable<ITimer> _timers;
        private readonly IOptions<ExplorePackagesSettings> _options;
        private readonly ITelemetryClient _telemetryClient;
        private readonly ILogger<TimerExecutionService> _logger;

        public TimerExecutionService(
            ServiceClientFactory serviceClientFactory,
            IEnumerable<ITimer> timers,
            IOptions<ExplorePackagesSettings> options,
            ITelemetryClient telemetryClient,
            ILogger<TimerExecutionService> logger)
        {
            var duplicateNames = timers
                .GroupBy(x => x.Name)
                .Where(g => g.Count() > 1)
                .Select(g => $"'{g.Key}' ({g.Count()})")
                .ToList();
            if (duplicateNames.Any())
            {
                throw new ArgumentException("There are timers with duplicate names: " + string.Join(", ", duplicateNames));
            }

            _serviceClientFactory = serviceClientFactory;
            _timers = timers;
            _options = options;
            _telemetryClient = telemetryClient;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await GetTable().CreateIfNotExistsAsync(retry: true);
            foreach (var timer in _timers)
            {
                await timer.InitializeAsync();
            }
        }

        public async Task SetIsEnabled(string timerName, bool isEnabled)
        {
            if (!_timers.Any(x => x.Name == timerName))
            {
                throw new ArgumentException("The provided timer name is not recognized.", nameof(timerName));
            }

            var table = GetTable();
            var entity = new TimerEntity(timerName) { IsEnabled = isEnabled };
            await table.ExecuteAsync(TableOperation.InsertOrMerge(entity));
        }

        public async Task ExecuteAsync(bool isEnabledDefault)
        {
            // Get the existing timer entities.
            var table = GetTable();
            var entities = await table.GetEntitiesAsync<TimerEntity>(
                PartitionKey,
                _telemetryClient.StartQueryLoopMetrics());
            var nameToEntity = entities.ToDictionary(x => x.RowKey);

            // Determine what to do for each timer.
            var toExecute = new List<(ITimer timer, TimerEntity entity)>();
            var batch = new TableBatchOperation();
            foreach (var timer in _timers)
            {
                if (!timer.IsEnabled)
                {
                    _logger.LogInformation("Timer {Name} will not be run because it is disabled in code.", timer.Name);
                }
                else if (!nameToEntity.TryGetValue(timer.Name, out var entity))
                {
                    entity = new TimerEntity(timer.Name) { IsEnabled = isEnabledDefault };
                    batch.Insert(entity);

                    if (isEnabledDefault)
                    {
                        toExecute.Add((timer, entity));
                        _logger.LogInformation("Timer {Name} will be run for the first time.");
                    }
                    else
                    {
                        _logger.LogInformation("Timer {Name} will be initialized without running.");
                    }
                }
                else if (!entity.IsEnabled)
                {
                    _logger.LogInformation("Timer {Name} will not be run because it is disabled in storage.", timer.Name);
                }
                else if (!entity.LastExecuted.HasValue)
                {
                    _logger.LogInformation("Timer {Name} will be run because it has never been run before.");
                    toExecute.Add((timer, entity));
                    batch.Replace(entity);
                }
                else if ((DateTimeOffset.UtcNow - entity.LastExecuted.Value) < timer.Frequency)
                {
                    _logger.LogInformation("Timer {Name} will not be run because it has been executed too recently.");
                }
                else
                {
                    _logger.LogInformation("Timer {Name} will be run because it has hasn't been run recently enough.");
                    toExecute.Add((timer, entity));
                    batch.Replace(entity);
                }
            }

            // Execute all timers.
            await Task.WhenAll(toExecute.Select(x => ExecuteAsync(x.timer, x.entity)));

            if (batch.Count > 0)
            {
                // Update table storage after the execute. In other words, if Table Storage fails, we could run the timers
                // too frequently.
                await table.ExecuteBatchAsync(batch);
            }
        }

        private async Task ExecuteAsync(ITimer timer, TimerEntity entity)
        {
            try
            {
                await timer.ExecuteAsync();
                _logger.LogInformation("Timer {Name} was executed successfully.", timer.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Timer {Name} failed to execute.", timer.Name);
            }

            entity.LastExecuted = DateTimeOffset.UtcNow;
        }

        private CloudTable GetTable()
        {
            return _serviceClientFactory
                .GetStorageAccount()
                .CreateCloudTableClient()
                .GetTableReference(_options.Value.TimerTableName);
        }
    }
}

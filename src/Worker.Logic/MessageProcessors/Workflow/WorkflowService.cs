// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using NuGet.Insights.Worker.AuxiliaryFileUpdater;
using NuGet.Insights.Worker.KustoIngestion;
using NuGet.Insights.Worker.ReferenceTracking;
using NuGet.Insights.Worker.TimedReprocess;

namespace NuGet.Insights.Worker.Workflow
{
    public class WorkflowService
    {
        private readonly WorkflowStorageService _workflowStorageService;
        private readonly AutoRenewingStorageLeaseService _leaseService;
        private readonly SpecificTimerExecutionService _timerExecutionService;
        private readonly TimedReprocessTimer _timedReprocessTimer;
        private readonly CatalogScanUpdateTimer _catalogScanUpdateTimer;
        private readonly IReadOnlyList<ICleanupOrphanRecordsTimer> _cleanupOrphanRecordsTimers;
        private readonly IReadOnlyList<IAuxiliaryFileUpdaterTimer> _auxiliaryFileUpdaterTimers;
        private readonly KustoIngestionTimer _kustoIngestionTimer;
        private readonly IReadOnlyList<ITimer> _allTimers;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly ILogger<WorkflowService> _logger;

        public WorkflowService(
            WorkflowStorageService workflowStorageService,
            AutoRenewingStorageLeaseService leaseService,
            SpecificTimerExecutionService timerExecutionService,
            TimedReprocessTimer timedReprocessTimer,
            CatalogScanUpdateTimer catalogScanUpdateTimer,
            IEnumerable<ICleanupOrphanRecordsTimer> cleanupOrphanRecordsTimers,
            IEnumerable<IAuxiliaryFileUpdaterTimer> auxiliaryFileUpdaterTimers,
            KustoIngestionTimer kustoIngestionTimer,
            IMessageEnqueuer messageEnqueuer,
            ILogger<WorkflowService> logger)
        {
            _workflowStorageService = workflowStorageService;
            _leaseService = leaseService;
            _timerExecutionService = timerExecutionService;
            _timedReprocessTimer = timedReprocessTimer;
            _catalogScanUpdateTimer = catalogScanUpdateTimer;
            _cleanupOrphanRecordsTimers = cleanupOrphanRecordsTimers.ToList();
            _auxiliaryFileUpdaterTimers = auxiliaryFileUpdaterTimers.ToList();
            _kustoIngestionTimer = kustoIngestionTimer;
            var timers = new List<ITimer> { _timedReprocessTimer, _catalogScanUpdateTimer };
            timers.AddRange(_cleanupOrphanRecordsTimers);
            timers.AddRange(_auxiliaryFileUpdaterTimers);
            timers.Add(_kustoIngestionTimer);
            _allTimers = timers;
            _messageEnqueuer = messageEnqueuer;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await Task.WhenAll(
                _workflowStorageService.InitializeAsync(),
                _leaseService.InitializeAsync(),
                _messageEnqueuer.InitializeAsync(),
                _timerExecutionService.InitializeAsync(),
                Task.WhenAll(_allTimers.Select(x => x.InitializeAsync())));
        }

        public async Task AbortAsync()
        {
            var runs = await _workflowStorageService.GetRunsAsync();
            var latestRun = runs.MaxBy(x => x.Created);
            if (latestRun is null || latestRun.State.IsTerminal())
            {
                return;
            }

            foreach (var timer in _allTimers)
            {
                if (timer.CanAbort)
                {
                    await timer.AbortAsync();
                }
            }

            latestRun.ETag = ETag.All;
            latestRun.Completed = DateTimeOffset.UtcNow;
            latestRun.State = WorkflowRunState.Aborted;
            await _workflowStorageService.ReplaceRunAsync(latestRun);
        }

        public async Task<WorkflowRun> StartAsync()
        {
            await using var lease = await _leaseService.TryAcquireWithRetryAsync("Start-Workflow");
            if (!lease.Acquired)
            {
                return null;
            }

            if (await IsAnyWorkflowStepRunningAsync())
            {
                _logger.LogTransientWarning("A workflow step is already running. A new workflow will not start.");
                return null;
            }

            var run = new WorkflowRun(StorageUtility.GenerateDescendingId().ToString())
            {
                Created = DateTimeOffset.UtcNow,
                AttemptCount = 1,
            };
            await _messageEnqueuer.EnqueueAsync(new[]
            {
                new WorkflowRunMessage
                {
                    RunId = run.RunId,
                }
            });
            await _workflowStorageService.AddRunAsync(run);
            return run;
        }

        public async Task<bool> IsWorkflowRunningAsync()
        {
            var runs = await _workflowStorageService.GetRunsAsync();
            return runs.Any(x => !x.State.IsTerminal());
        }

        public async Task<bool> IsAnyWorkflowStepRunningAsync()
        {
            return await IsWorkflowRunningAsync() || await IsAnyTimerRunningAsync();
        }

        private async Task<bool> IsAnyTimerRunningAsync()
        {
            foreach (var timer in _allTimers)
            {
                if (await timer.IsRunningAsync())
                {
                    return true;
                }
            }

            return false;
        }

        internal async Task StartTimedReprocessAsync()
        {
            await StartTimerAsync(_timedReprocessTimer);
        }

        internal async Task StartCatalogScansAsync()
        {
            await StartTimerAsync(_catalogScanUpdateTimer);
        }

        internal async Task StartCleanupOrphanRecordsAsync()
        {
            foreach (var timer in _cleanupOrphanRecordsTimers)
            {
                await StartTimerAsync(timer);
            }
        }

        internal async Task StartAuxiliaryFilesAsync()
        {
            foreach (var timer in _auxiliaryFileUpdaterTimers)
            {
                await StartTimerAsync(timer);
            }
        }

        internal async Task StartKustoIngestionAsync()
        {
            await StartTimerAsync(_kustoIngestionTimer);
        }

        private async Task StartTimerAsync(ITimer timer)
        {
            if (!timer.IsEnabled)
            {
                _logger.LogInformation("The {TimerName} timer does not have the required configuration and will be skipped.", timer.Name);
                return;
            }

            if (await timer.IsRunningAsync())
            {
                _logger.LogInformation("The {TimerName} timer is already running.", timer.Name);
                return;
            }

            _logger.LogInformation("Starting the {TimerName} timer.", timer.Name);
            var started = await _timerExecutionService.ExecuteAsync(new[] { timer }, executeNow: true);
            if (!started)
            {
                throw new InvalidOperationException($"The {timer.Name} timer could not be started.");
            }
        }

        internal async Task<bool> IsTimedReprocessRunningAsync()
        {
            return await _timedReprocessTimer.IsRunningAsync();
        }

        internal async Task<bool> AreCatalogScansRunningAsync()
        {
            return await _catalogScanUpdateTimer.IsRunningAsync();
        }

        internal async Task<bool> AreCleanupOrphanRecordsRunningAsync()
        {
            foreach (var timer in _cleanupOrphanRecordsTimers)
            {
                if (await timer.IsRunningAsync())
                {
                    return true;
                }
            }

            return false;
        }

        internal async Task<bool> AreAuxiliaryFilesRunningAsync()
        {
            foreach (var timer in _auxiliaryFileUpdaterTimers)
            {
                if (await timer.IsRunningAsync())
                {
                    return true;
                }
            }

            return false;
        }

        internal async Task<bool> IsKustoIngestionRunningAsync()
        {
            return await _kustoIngestionTimer.IsRunningAsync();
        }
    }
}

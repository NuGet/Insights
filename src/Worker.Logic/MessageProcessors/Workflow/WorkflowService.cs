// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Insights.Worker.AuxiliaryFileUpdater;
using NuGet.Insights.Worker.KustoIngestion;
using NuGet.Insights.Worker.ReferenceTracking;

namespace NuGet.Insights.Worker.Workflow
{
    public class WorkflowService
    {
        private readonly WorkflowStorageService _workflowStorageService;
        private readonly AutoRenewingStorageLeaseService _leaseService;
        private readonly SpecificTimerExecutionService _timerExecutionService;
        private readonly CatalogScanUpdateTimer _catalogScanUpdateTimer;
        private readonly IReadOnlyList<ICleanupOrphanRecordsTimer> _cleanupOrphanRecordsTimers;
        private readonly IReadOnlyList<IAuxiliaryFileUpdaterTimer> _auxiliaryFileUpdaterTimers;
        private readonly KustoIngestionTimer _kustoIngestionTimer;
        private readonly IMessageEnqueuer _messageEnqueuer;

        public WorkflowService(
            WorkflowStorageService workflowStorageService,
            AutoRenewingStorageLeaseService leaseService,
            SpecificTimerExecutionService timerExecutionService,
            CatalogScanUpdateTimer catalogScanUpdateTimer,
            IEnumerable<ICleanupOrphanRecordsTimer> cleanupOrphanRecordsTimers,
            IEnumerable<IAuxiliaryFileUpdaterTimer> auxiliaryFileUpdaterTimers,
            KustoIngestionTimer kustoIngestionTimer,
            IMessageEnqueuer messageEnqueuer)
        {
            _workflowStorageService = workflowStorageService;
            _leaseService = leaseService;
            _timerExecutionService = timerExecutionService;
            _catalogScanUpdateTimer = catalogScanUpdateTimer;
            _cleanupOrphanRecordsTimers = cleanupOrphanRecordsTimers.ToList();
            _auxiliaryFileUpdaterTimers = auxiliaryFileUpdaterTimers.ToList();
            _kustoIngestionTimer = kustoIngestionTimer;
            _messageEnqueuer = messageEnqueuer;
        }

        public async Task InitializeAsync()
        {
            await _workflowStorageService.InitializeAsync();
            await _leaseService.InitializeAsync();
            await _messageEnqueuer.InitializeAsync();
            await _timerExecutionService.InitializeAsync(Enumerable.Empty<ITimer>()
                .Concat(new ITimer[] { _catalogScanUpdateTimer, _kustoIngestionTimer })
                .Concat(_cleanupOrphanRecordsTimers)
                .Concat(_auxiliaryFileUpdaterTimers));
        }

        public bool HasRequiredConfiguration => _catalogScanUpdateTimer.IsEnabled
            && _cleanupOrphanRecordsTimers.All(x => x.IsEnabled)
            && _auxiliaryFileUpdaterTimers.All(x => x.IsEnabled)
            && _kustoIngestionTimer.IsEnabled;

        public async Task<WorkflowRun> StartAsync()
        {
            await using var lease = await _leaseService.TryAcquireAsync("Start-Workflow");
            if (!lease.Acquired)
            {
                return null;
            }

            if (await IsAnyWorkflowStepRunningAsync())
            {
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
                    WorkflowRunId = run.GetRunId(),
                }
            });
            await _workflowStorageService.AddRunAsync(run);
            return run;
        }

        public async Task<bool> IsWorkflowRunningAsync()
        {
            var runs = await _workflowStorageService.GetRunsAsync();
            return runs.Any(x => x.State != WorkflowRunState.Complete);
        }

        public async Task<bool> IsAnyWorkflowStepRunningAsync()
        {
            return await IsWorkflowRunningAsync()
                || await AreCatalogScansRunningAsync()
                || await AreCleanupOrphanRecordsRunningAsync()
                || await AreAuxiliaryFilesRunningAsync()
                || await IsKustoIngestionRunningAsync();
        }

        internal async Task StartCatalogScansAsync()
        {
            await _timerExecutionService.ExecuteAsync(new[] { _catalogScanUpdateTimer }, executeNow: true);
        }

        internal async Task StartCleanupOrphanRecordsAsync()
        {
            await _timerExecutionService.ExecuteAsync(_cleanupOrphanRecordsTimers, executeNow: true);
        }

        internal async Task StartAuxiliaryFilesAsync()
        {
            await _timerExecutionService.ExecuteAsync(_auxiliaryFileUpdaterTimers, executeNow: true);
        }

        internal async Task StartKustoIngestionAsync()
        {
            await _timerExecutionService.ExecuteAsync(new[] { _kustoIngestionTimer }, executeNow: true);
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

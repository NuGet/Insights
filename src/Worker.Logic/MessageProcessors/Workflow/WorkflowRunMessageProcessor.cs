// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Insights.Worker.KustoIngestion;

namespace NuGet.Insights.Worker.Workflow
{
    public class WorkflowRunMessageProcessor : IMessageProcessor<WorkflowRunMessage>
    {
        private static readonly IReadOnlyList<WorkflowStateTransition> Transitions = new[]
        {
            new WorkflowStateTransition(
                CurrentState: WorkflowRunState.Created,
                IsIncompleteAsync: self => Task.FromResult(false),
                TransitionAsync: async (self, run) =>
                {
                    self._logger.LogInformation("Starting all catalog scans.");
                    await self._workflowService.StartCatalogScansAsync();
                    return WorkflowRunState.CatalogScanWorking;
                }),

            new WorkflowStateTransition(
                CurrentState: WorkflowRunState.CatalogScanWorking,
                IsIncompleteAsync: self => self._workflowService.AreCatalogScansRunningAsync(),
                TransitionAsync: async (self, run) =>
                {
                    self._logger.LogInformation("Starting cleanup of orphan records.");
                    await self._workflowService.StartCleanupOrphanRecordsAsync();
                    return WorkflowRunState.CleanupOrphanRecordsWorking;
                }),

            new WorkflowStateTransition(
                CurrentState: WorkflowRunState.CleanupOrphanRecordsWorking,
                IsIncompleteAsync: self => self._workflowService.AreCleanupOrphanRecordsRunningAsync(),
                TransitionAsync: async (self, run) =>
                {
                    self._logger.LogInformation("Starting auxiliary file processors.");
                    await self._workflowService.StartAuxiliaryFilesAsync();
                    return WorkflowRunState.AuxiliaryFilesWorking;
                }),

            new WorkflowStateTransition(
                CurrentState: WorkflowRunState.AuxiliaryFilesWorking,
                IsIncompleteAsync: self => self._workflowService.AreAuxiliaryFilesRunningAsync(),
                TransitionAsync: async (self, run) =>
                {
                    self._logger.LogInformation("Starting Kusto ingestion.");
                    await self._workflowService.StartKustoIngestionAsync();
                    return WorkflowRunState.KustoIngestionWorking;
                }),

            new WorkflowStateTransition(
                CurrentState: WorkflowRunState.KustoIngestionWorking,
                IsIncompleteAsync: self => self._workflowService.IsKustoIngestionRunningAsync(),
                TransitionAsync: async (self, run) =>
                {
                    var latestState = await self._kustoIngestionStorageService.GetLatestStateAsync();
                    if (latestState != KustoIngestionState.Complete)
                    {
                        if (run.AttemptCount >= self._options.Value.WorkflowMaxAttempts)
                        {
                            throw new InvalidOperationException($"The workflow could not complete due to Kusto {latestState} state after {run.AttemptCount} attempts.");
                        }

                        self._logger.LogWarning("Retrying the entire workflow due to Kusto {latestState} state.", latestState);
                        run.AttemptCount++;
                        return WorkflowRunState.Created;
                    }

                    return WorkflowRunState.Finalizing;
                }),

            new WorkflowStateTransition(
                CurrentState: WorkflowRunState.Finalizing,
                IsIncompleteAsync: self => Task.FromResult(false),
                TransitionAsync: async (self, run) =>
                {
                    await self._storageService.DeleteOldRunsAsync(run.GetRunId());
                    self._logger.LogInformation("The workflow is complete.");
                    run.Completed = DateTimeOffset.UtcNow;
                    return WorkflowRunState.Complete;
                }),
        };

        private static readonly IReadOnlyDictionary<WorkflowRunState, WorkflowStateTransition> CurrentStateToTransition = Transitions
            .ToDictionary(x => x.CurrentState);

        private readonly WorkflowService _workflowService;
        private readonly WorkflowStorageService _storageService;
        private readonly KustoIngestionStorageService _kustoIngestionStorageService;
        private readonly AutoRenewingStorageLeaseService _leaseService;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly ITelemetryClient _telemetryClient;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ILogger<WorkflowRunMessageProcessor> _logger;

        public WorkflowRunMessageProcessor(
            WorkflowService workflowService,
            WorkflowStorageService storageService,
            KustoIngestionStorageService kustoIngestionStorageService,
            AutoRenewingStorageLeaseService leaseService,
            IMessageEnqueuer messageEnqueuer,
            ITelemetryClient telemetryClient,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger<WorkflowRunMessageProcessor> logger)
        {
            _workflowService = workflowService;
            _storageService = storageService;
            _kustoIngestionStorageService = kustoIngestionStorageService;
            _leaseService = leaseService;
            _messageEnqueuer = messageEnqueuer;
            _telemetryClient = telemetryClient;
            _options = options;
            _logger = logger;
        }

        public async Task ProcessAsync(WorkflowRunMessage message, long dequeueCount)
        {
            var run = await _storageService.GetRunAsync(message.WorkflowRunId);
            if (run == null)
            {
                await Task.Delay(TimeSpan.FromSeconds(dequeueCount * 15));
                throw new InvalidOperationException($"An incomplete workflow run for {message.WorkflowRunId} should have already been created.");
            }

            await using var lease = await LeaseOrNullAsync(message);
            if (lease == null)
            {
                return;
            }

            _logger.LogInformation("Processing workflow run {RunId}.", run.GetRunId());

            while (run.State != WorkflowRunState.Complete)
            {
                var transition = CurrentStateToTransition[run.State];
                if (await transition.IsIncompleteAsync(this))
                {
                    _logger.LogInformation("The {State} is not yet complete.", run.State);
                    message.AttemptCount++;
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, TimeSpan.FromSeconds(5));
                    return;
                }
                else
                {
                    var nextState = await transition.TransitionAsync(this, run);

                    _telemetryClient.TrackMetric(
                        "Workflow.StateTransition",
                        1,
                        new Dictionary<string, string> { { "NextState", nextState.ToString() } });
                    run.State = nextState;

                    await _storageService.ReplaceRunAsync(run);
                }
            }
        }

        private async Task<IAsyncDisposable> LeaseOrNullAsync(WorkflowRunMessage message)
        {
            var lease = await _leaseService.TryAcquireAsync(nameof(WorkflowRunMessageProcessor));
            if (!lease.Acquired)
            {
                _logger.LogTransientWarning("The lease for workflow run processing is not available.");
                message.AttemptCount++;
                await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount));
                return null;
            }

            return lease;
        }

        private record WorkflowStateTransition(
            WorkflowRunState CurrentState,
            Func<WorkflowRunMessageProcessor, Task<bool>> IsIncompleteAsync,
            Func<WorkflowRunMessageProcessor, WorkflowRun, Task<WorkflowRunState>> TransitionAsync);
    }
}

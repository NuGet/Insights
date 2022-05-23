// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

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
                },
                NextState: WorkflowRunState.CatalogScanWorking),

            new WorkflowStateTransition(
                CurrentState: WorkflowRunState.CatalogScanWorking,
                IsIncompleteAsync: self => self._workflowService.AreCatalogScansRunningAsync(),
                TransitionAsync: async (self, run) =>
                {
                    self._logger.LogInformation("Starting cleanup of orphan records.");
                    await self._workflowService.StartCleanupOrphanRecordsAsync();
                },
                NextState: WorkflowRunState.CleanupOrphanRecordsWorking),

            new WorkflowStateTransition(
                CurrentState: WorkflowRunState.CleanupOrphanRecordsWorking,
                IsIncompleteAsync: self => self._workflowService.AreCleanupOrphanRecordsRunningAsync(),
                TransitionAsync: async (self, run) =>
                {
                    self._logger.LogInformation("Starting auxiliary file processors.");
                    await self._workflowService.StartAuxiliaryFilesAsync();
                },
                NextState: WorkflowRunState.AuxiliaryFilesWorking),

            new WorkflowStateTransition(
                CurrentState: WorkflowRunState.AuxiliaryFilesWorking,
                IsIncompleteAsync: self => self._workflowService.AreAuxiliaryFilesRunningAsync(),
                TransitionAsync: async (self, run) =>
                {
                    self._logger.LogInformation("Starting Kusto ingestion.");
                    await self._workflowService.StartKustoIngestionAsync();
                },
                NextState: WorkflowRunState.KustoIngestionWorking),

            new WorkflowStateTransition(
                CurrentState: WorkflowRunState.KustoIngestionWorking,
                IsIncompleteAsync: self => self._workflowService.IsKustoIngestionRunningAsync(),
                TransitionAsync: (self, run) =>
                {
                    return Task.CompletedTask;
                },
                NextState: WorkflowRunState.Finalizing),

            new WorkflowStateTransition(
                CurrentState: WorkflowRunState.Finalizing,
                IsIncompleteAsync: self => Task.FromResult(false),
                TransitionAsync: async (self, run) =>
                {
                    await self._storageService.DeleteOldRunsAsync(run.GetRunId());

                    self._logger.LogInformation("The workflow is complete.");
                    run.Completed = DateTimeOffset.UtcNow;
                },
                NextState: WorkflowRunState.Complete),
        };

        private static readonly IReadOnlyDictionary<WorkflowRunState, WorkflowStateTransition> CurrentStateToTransition = Transitions
            .ToDictionary(x => x.CurrentState);

        private readonly WorkflowService _workflowService;
        private readonly WorkflowStorageService _storageService;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly ITelemetryClient _telemetryClient;
        private readonly ILogger<WorkflowRunMessageProcessor> _logger;

        public WorkflowRunMessageProcessor(
            WorkflowService workflowService,
            WorkflowStorageService storageService,
            IMessageEnqueuer messageEnqueuer,
            ITelemetryClient telemetryClient,
            ILogger<WorkflowRunMessageProcessor> logger)
        {
            _workflowService = workflowService;
            _storageService = storageService;
            _messageEnqueuer = messageEnqueuer;
            _telemetryClient = telemetryClient;
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
                    _telemetryClient.TrackMetric(
                        "Workflow.StateTransition",
                        1,
                        new Dictionary<string, string> { { "NextState", transition.NextState.ToString() } });
                    await transition.TransitionAsync(this, run);

                    run.State = transition.NextState;
                    await _storageService.ReplaceRunAsync(run);
                }
            }
        }

        private record WorkflowStateTransition(
            WorkflowRunState CurrentState,
            Func<WorkflowRunMessageProcessor, Task<bool>> IsIncompleteAsync,
            Func<WorkflowRunMessageProcessor, WorkflowRun, Task> TransitionAsync,
            WorkflowRunState NextState);
    }
}

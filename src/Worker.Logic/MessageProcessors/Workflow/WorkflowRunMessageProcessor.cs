using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Worker.KustoIngestion;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Worker.Workflow
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
                    await self._catalogScanService.UpdateAllAsync(run.MaxCommitTimestamp);
                },
                NextState: WorkflowRunState.CatalogScanWorking),

            new WorkflowStateTransition(
                CurrentState: WorkflowRunState.CatalogScanWorking,
                IsIncompleteAsync: self => self._workflowService.AreCatalogScansRunningAsync(),
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
                    await self._kustoIngestionService.StartAsync();
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
        private readonly CatalogScanService _catalogScanService;
        private readonly KustoIngestionService _kustoIngestionService;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly ILogger<WorkflowRunMessageProcessor> _logger;

        public WorkflowRunMessageProcessor(
            WorkflowService workflowService,
            WorkflowStorageService storageService,
            CatalogScanService catalogScanService,
            KustoIngestionService kustoIngestionService,
            IMessageEnqueuer messageEnqueuer,
            ILogger<WorkflowRunMessageProcessor> logger)
        {
            _workflowService = workflowService;
            _storageService = storageService;
            _catalogScanService = catalogScanService;
            _kustoIngestionService = kustoIngestionService;
            _messageEnqueuer = messageEnqueuer;
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
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount));
                    return;
                }
                else
                {
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

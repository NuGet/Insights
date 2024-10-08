// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
                    await self._workflowService.StartCatalogScansAsync();
                    return WorkflowRunState.CatalogScanWorking;
                }),

            new WorkflowStateTransition(
                CurrentState: WorkflowRunState.CatalogScanWorking,
                IsIncompleteAsync: self => self._workflowService.AreCatalogScansRunningAsync(),
                TransitionAsync: async (self, run) =>
                {
                    await self._workflowService.StartCleanupOrphanRecordsAsync();
                    return WorkflowRunState.CleanupOrphanRecordsWorking;
                }),

            new WorkflowStateTransition(
                CurrentState: WorkflowRunState.CleanupOrphanRecordsWorking,
                IsIncompleteAsync: self => self._workflowService.AreCleanupOrphanRecordsRunningAsync(),
                TransitionAsync: async (self, run) =>
                {
                    await self._workflowService.StartTimedReprocessAsync();
                    return WorkflowRunState.TimedReprocessWorking;
                }),

            new WorkflowStateTransition(
                CurrentState: WorkflowRunState.TimedReprocessWorking,
                IsIncompleteAsync: self => self._workflowService.IsTimedReprocessRunningAsync(),
                TransitionAsync: async (self, run) =>
                {
                    await self._workflowService.StartAuxiliaryFilesAsync();
                    return WorkflowRunState.AuxiliaryFilesWorking;
                }),

            new WorkflowStateTransition(
                CurrentState: WorkflowRunState.AuxiliaryFilesWorking,
                IsIncompleteAsync: self => self._workflowService.AreAuxiliaryFilesRunningAsync(),
                TransitionAsync: async (self, run) =>
                {
                    await self._workflowService.StartKustoIngestionAsync();
                    return WorkflowRunState.KustoIngestionWorking;
                }),

            new WorkflowStateTransition(
                CurrentState: WorkflowRunState.KustoIngestionWorking,
                IsIncompleteAsync: self => self._workflowService.IsKustoIngestionRunningAsync(),
                TransitionAsync: async (self, run) =>
                {
                    var latestState = await self._kustoIngestionStorageService.GetLatestStateAsync();
                    if (latestState.HasValue)
                    {
                        if (latestState == KustoIngestionState.FailedValidation)
                        {
                            if (run.AttemptCount >= self._options.Value.WorkflowMaxAttempts)
                            {
                                throw new InvalidOperationException($"The workflow could not complete due to Kusto {latestState} state after {run.AttemptCount} attempts.");
                            }

                            self._logger.LogWarning("Retrying the entire workflow due to Kusto {latestState} state.", latestState);
                            run.AttemptCount++;
                            return WorkflowRunState.Created;
                        }
                        else if (latestState != KustoIngestionState.Complete)
                        {
                            throw new InvalidOperationException("Unexpected workflow state: " + latestState);
                        }
                    }

                    return WorkflowRunState.Finalizing;
                }),

            new WorkflowStateTransition(
                CurrentState: WorkflowRunState.Finalizing,
                IsIncompleteAsync: self => Task.FromResult(false),
                TransitionAsync: async (self, run) =>
                {
                    await self.FinalizeAsync(run);

                    return WorkflowRunState.Complete;
                }),
        };

        private async Task FinalizeAsync(WorkflowRun run)
        {
            await _storageService.DeleteOldRunsAsync(run.RunId);
            _logger.LogInformation("The workflow is complete.");

            await EmitCsvBlobMetricsAsync();

            run.Completed = DateTimeOffset.UtcNow;
        }

        private async Task EmitCsvBlobMetricsAsync()
        {
            var countMetric = _telemetryClient.GetMetric(MetricNames.CsvBlobCount, "ContainerName");
            var recordCountMetric = _telemetryClient.GetMetric(MetricNames.CsvBlobRecordCount, "ContainerName");
            var compressedSizeMetric = _telemetryClient.GetMetric(MetricNames.CsvBlobCompressedSize, "ContainerName");
            var uncompressedSizeMetric = _telemetryClient.GetMetric(MetricNames.CsvBlobUncompressedSize, "ContainerName");

            foreach (var containerName in _csvRecordContainers.ContainerNames)
            {
                var blobs = await _csvRecordContainers.GetBlobsAsync(containerName);
                countMetric.TrackValue(blobs.Count, containerName);
                foreach (var blob in blobs)
                {
                    if (blob.RecordCount.HasValue)
                    {
                        recordCountMetric.TrackValue(blob.RecordCount.Value, containerName);
                    }

                    compressedSizeMetric.TrackValue(blob.CompressedSizeBytes, containerName);

                    if (blob.RawSizeBytes.HasValue)
                    {
                        uncompressedSizeMetric.TrackValue(blob.RawSizeBytes.Value, containerName);
                    }
                }
            }
        }

        private static readonly IReadOnlyDictionary<WorkflowRunState, WorkflowStateTransition> CurrentStateToTransition = Transitions
            .ToDictionary(x => x.CurrentState);

        private readonly WorkflowService _workflowService;
        private readonly WorkflowStorageService _storageService;
        private readonly CsvRecordContainers _csvRecordContainers;
        private readonly KustoIngestionStorageService _kustoIngestionStorageService;
        private readonly AutoRenewingStorageLeaseService _leaseService;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly ITelemetryClient _telemetryClient;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ILogger<WorkflowRunMessageProcessor> _logger;

        public WorkflowRunMessageProcessor(
            WorkflowService workflowService,
            WorkflowStorageService storageService,
            CsvRecordContainers csvRecordContainers,
            KustoIngestionStorageService kustoIngestionStorageService,
            AutoRenewingStorageLeaseService leaseService,
            IMessageEnqueuer messageEnqueuer,
            ITelemetryClient telemetryClient,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger<WorkflowRunMessageProcessor> logger)
        {
            _workflowService = workflowService;
            _storageService = storageService;
            _csvRecordContainers = csvRecordContainers;
            _kustoIngestionStorageService = kustoIngestionStorageService;
            _leaseService = leaseService;
            _messageEnqueuer = messageEnqueuer;
            _telemetryClient = telemetryClient;
            _options = options;
            _logger = logger;
        }

        public async Task ProcessAsync(WorkflowRunMessage message, long dequeueCount)
        {
            var run = await _storageService.GetRunAsync(message.RunId);
            if (run == null)
            {
                if (message.AttemptCount < 10)
                {
                    _logger.LogTransientWarning("After {AttemptCount} attempts, the workflow run {RunId} should have already been created. Trying again.",
                        message.AttemptCount,
                        message.RunId);
                    message.AttemptCount++;
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount));
                }
                else
                {
                    _logger.LogTransientWarning("After {AttemptCount} attempts, the workflow run {RunId} should have already been created. Giving up.",
                        message.AttemptCount,
                        message.RunId);
                }

                return;
            }

            await using var lease = await LeaseOrNullAsync(message);
            if (lease == null)
            {
                return;
            }

            _logger.LogInformation("Processing workflow run {RunId}.", run.RunId);

            while (!run.State.IsTerminal())
            {
                var transition = CurrentStateToTransition[run.State];
                if (await transition.IsIncompleteAsync(this))
                {
                    _logger.LogInformation("The {State} state is not yet complete.", run.State);
                    message.AttemptCount++;
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, TimeSpan.FromSeconds(5));
                    return;
                }
                else
                {
                    var nextState = await transition.TransitionAsync(this, run);

                    _telemetryClient.TrackMetric(
                        MetricNames.WorkflowStateTransition,
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

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.Workflow;

namespace NuGet.Insights.Worker
{
    /// <summary>
    /// This is not implemented as an <see cref="ITimer"/> so it can be explicitly invoked more frequently with less
    /// concern for concurrency or tracking.
    /// </summary>
    public class MetricsTimer
    {
        private readonly WorkflowStorageService _workflowStorageService;
        private readonly IRawMessageEnqueuer _messageEnqueuer;
        private readonly ITelemetryClient _telemetryClient;

        public MetricsTimer(
            WorkflowStorageService workflowStorageService,
            IRawMessageEnqueuer messageEnqueuer,
            ITelemetryClient telemetryClient)
        {
            _workflowStorageService = workflowStorageService;
            _messageEnqueuer = messageEnqueuer;
            _telemetryClient = telemetryClient;
        }

        public async Task ExecuteAsync()
        {
            await EmitWorkflowMetricsAsync();
            await EmitQueueSizeMetricsAsync();
        }

        private async Task EmitWorkflowMetricsAsync()
        {
            var runs = await _workflowStorageService.GetRunsAsync();
            var latestCompletedRun = runs
                .Where(x => x.State == WorkflowRunState.Complete)
                .OrderByDescending(x => x.Completed.Value)
                .FirstOrDefault();
            TimeSpan sinceLastCompletion;
            if (latestCompletedRun is null)
            {
                // Take an arbitrarily long value if there are no runs at all.
                sinceLastCompletion = TimeSpan.FromDays(7);
            }
            else
            {
                sinceLastCompletion = DateTimeOffset.UtcNow - latestCompletedRun.Completed.Value;
            }

            _telemetryClient.TrackMetric(MetricNames.SinceLastWorkflowCompletedHours, sinceLastCompletion.TotalHours);
        }

        private async Task EmitQueueSizeMetricsAsync()
        {
            var main = 0;
            var poison = 0;
            foreach (var x in Enum.GetValues<QueueType>())
            {
                main += await EmitQueueSizeAsync(_messageEnqueuer.GetApproximateMessageCountAsync(x), x, isPoison: false);
                poison += await EmitQueueSizeAsync(_messageEnqueuer.GetPoisonApproximateMessageCountAsync(x), x, isPoison: true);
            }

            _telemetryClient.TrackMetric(MetricNames.StorageQueueSizeMain, main);
            _telemetryClient.TrackMetric(MetricNames.StorageQueueSizeExpandPoison, poison);
            _telemetryClient.TrackMetric(MetricNames.StorageQueueSize, main + poison);
        }

        private async Task<int> EmitQueueSizeAsync(Task<int> countAsync, QueueType queue, bool isPoison)
        {
            var count = await countAsync;
            var metricName = (queue, isPoison) switch
            {
                (QueueType.Work, false) => MetricNames.StorageQueueSizeWorkMain,
                (QueueType.Work, true) => MetricNames.StorageQueueSizeWorkPoison,
                (QueueType.Expand, false) => MetricNames.StorageQueueSizeExpandMain,
                (QueueType.Expand, true) => MetricNames.StorageQueueSizeExpandPoison,
                _ => throw new NotImplementedException(),
            };

            _telemetryClient.TrackMetric(metricName, count);
            return count;
        }

        public async Task InitializeAsync()
        {
            await _workflowStorageService.InitializeAsync();
            await _messageEnqueuer.InitializeAsync();
        }
    }
}

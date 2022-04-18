// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Insights.Worker.Workflow;

namespace NuGet.Insights.Worker
{
    public class MetricsTimer : ITimer
    {
        private static readonly IDictionary<string, string> NoDimensions = new Dictionary<string, string>();
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

        public string Name => "Metrics";
        public TimeSpan Frequency => TimeSpan.FromSeconds(1);
        public bool AutoStart => true;
        public bool IsEnabled => true;
        public int Order => default;

        public async Task<bool> ExecuteAsync()
        {
            await EmitWorkflowMetricsAsync();
            await EmitQueueSizeMetricsAsync();

            return true;
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

            TrackMetric("SinceLastWorkflowCompletedHours", sinceLastCompletion.TotalHours);
        }

        private void TrackMetric(string name, double value)
        {
            // We use TrackMetric instead of GetMetric here to immediately send the data.
            // GetMetric uses a 1 minute local aggregation period.
            // Source: https://docs.microsoft.com/en-us/azure/azure-monitor/app/get-metric
            _telemetryClient.TrackMetric(name, value, NoDimensions);
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

            TrackMetric("StorageQueueSize.Main", main);
            TrackMetric("StorageQueueSize.Poison", poison);
            TrackMetric("StorageQueueSize", main + poison);
        }

        private async Task<int> EmitQueueSizeAsync(Task<int> countAsync, QueueType queue, bool isPoison)
        {
            var count = await countAsync;
            TrackMetric($"StorageQueueSize.{queue}.{(isPoison ? "Poison" : "Main")}", count);
            return count;
        }

        public async Task InitializeAsync()
        {
            await _messageEnqueuer.InitializeAsync();
        }

        public Task<bool> IsRunningAsync()
        {
            return Task.FromResult(false);
        }
    }
}

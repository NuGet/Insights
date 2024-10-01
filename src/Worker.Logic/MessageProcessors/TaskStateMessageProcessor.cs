// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;

namespace NuGet.Insights.Worker
{
    public class TaskStateMessageProcessor<T> : IMessageProcessor<T> where T : ITaskStateMessage
    {
        public const string MetricIdPrefix = $"{nameof(TaskStateMessageProcessor<T>)}.";
        private static readonly string MessageTypeName = typeof(T).Name;

        private readonly TaskStateStorageService _taskStateStorageService;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly ITaskStateMessageProcessor<T> _processor;
        private readonly ITelemetryClient _telemetryClient;
        private readonly ILogger<TaskStateMessageProcessor<T>> _logger;
        private readonly IMetric _messageComplete;
        private readonly IMetric _messageConflict;
        private readonly IMetric _messageDelay;
        private readonly IMetric _messageContinue;
        private readonly IMetric _taskStateMissing;
        private readonly IMetric _taskStateDropped;

        public TaskStateMessageProcessor(
            TaskStateStorageService taskStateStorageService,
            IMessageEnqueuer messageEnqueuer,
            ITaskStateMessageProcessor<T> processor,
            ITelemetryClient telemetryClient,
            ILogger<TaskStateMessageProcessor<T>> logger)
        {
            _taskStateStorageService = taskStateStorageService;
            _messageEnqueuer = messageEnqueuer;
            _processor = processor;
            _telemetryClient = telemetryClient;
            _logger = logger;

            _messageComplete = _telemetryClient
                .GetMetric($"{MetricIdPrefix}MessageComplete", "MessageType", "IsDuplicate");
            _messageConflict = _telemetryClient
                .GetMetric($"{MetricIdPrefix}MessageConflict", "MessageType");
            _messageDelay = _telemetryClient
                .GetMetric($"{MetricIdPrefix}MessageRetry", "MessageType");
            _messageContinue = _telemetryClient
                .GetMetric($"{MetricIdPrefix}MessageContinue", "MessageType");
            _taskStateMissing = _telemetryClient
                .GetMetric($"{MetricIdPrefix}TaskStateMissingRetry", "MessageType");
            _taskStateDropped = _telemetryClient
                .GetMetric($"{MetricIdPrefix}TaskStateMissingDropped", "MessageType");
        }

        public async Task ProcessAsync(T message, long dequeueCount)
        {
            var taskState = await _taskStateStorageService.GetAsync(message.TaskStateKey);
            if (taskState is null)
            {
                if (message.AttemptCount < 10)
                {
                    _taskStateMissing.TrackValue(1, MessageTypeName);
                    _logger.LogTransientWarning(
                        "Attempt {AttemptCount}: no task state for storage suffix '{StorageSuffix}', {PartitionKey}, {RowKey} was found. Trying again.",
                        message.AttemptCount,
                        message.TaskStateKey.StorageSuffix,
                        message.TaskStateKey.PartitionKey,
                        message.TaskStateKey.RowKey);
                    message.AttemptCount++;
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount));
                }
                else
                {
                    _taskStateDropped.TrackValue(1, MessageTypeName);
                    _logger.LogTransientWarning(
                        "Attempt {AttemptCount}: no task state for storage suffix '{StorageSuffix}', {PartitionKey}, {RowKey} was found. Giving up.",
                        message.AttemptCount,
                        message.TaskStateKey.StorageSuffix,
                        message.TaskStateKey.PartitionKey,
                        message.TaskStateKey.RowKey);
                }

                return;
            }

            if (!taskState.Started.HasValue)
            {
                taskState.Started = DateTimeOffset.UtcNow;
                try
                {
                    await _taskStateStorageService.UpdateAsync(taskState);
                }
                catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.PreconditionFailed)
                {
                    _logger.LogTransientWarning(
                        "Attempt {AttemptCount}: task state for storage suffix '{StorageSuffix}', {PartitionKey}, {RowKey} was updated by another worker. Trying again.",
                        message.AttemptCount,
                        message.TaskStateKey.StorageSuffix,
                        message.TaskStateKey.PartitionKey,
                        message.TaskStateKey.RowKey);
                    _messageConflict.TrackValue(1, MessageTypeName);
                    message.AttemptCount++;
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount));
                    return;
                }
            }

            var result = await _processor.ProcessAsync(message, taskState, dequeueCount);
            switch (result)
            {
                case TaskStateProcessResult.Complete:
                    var deleted = await _taskStateStorageService.DeleteAsync(taskState);
                    _messageComplete.TrackValue(1, MessageTypeName, deleted ? "false" : "true");
                    break;
                case TaskStateProcessResult.Delay:
                    _messageDelay.TrackValue(1, MessageTypeName);
                    message.AttemptCount++;
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount));
                    break;
                case TaskStateProcessResult.Continue:
                    _messageContinue.TrackValue(1, MessageTypeName);
                    await _messageEnqueuer.EnqueueAsync(new[] { message });
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
    }
}

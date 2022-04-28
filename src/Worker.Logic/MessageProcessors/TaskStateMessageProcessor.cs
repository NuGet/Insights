// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Insights.Worker
{
    public class TaskStateMessageProcessor<T> : IMessageProcessor<T> where T : ITaskStateMessage
    {
        private readonly TaskStateStorageService _taskStateStorageService;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly ITaskStateMessageProcessor<T> _processor;
        private readonly ILogger<TaskStateMessageProcessor<T>> _logger;

        public TaskStateMessageProcessor(
            TaskStateStorageService taskStateStorageService,
            IMessageEnqueuer messageEnqueuer,
            ITaskStateMessageProcessor<T> processor,
            ILogger<TaskStateMessageProcessor<T>> logger)
        {
            _taskStateStorageService = taskStateStorageService;
            _messageEnqueuer = messageEnqueuer;
            _processor = processor;
            _logger = logger;
        }

        public async Task ProcessAsync(T message, long dequeueCount)
        {
            var taskState = await _taskStateStorageService.GetAsync(message.TaskStateKey);
            if (taskState == null)
            {
                message.AttemptCount++;
                if (message.AttemptCount <= 10)
                {
                    _logger.LogWarning(
                        "Attempt {AttemptCount}: no task state for {StorageSuffix}, {PartitionKey}, {RowKey} was found. Trying again.",
                        message.AttemptCount,
                        message.TaskStateKey.StorageSuffix,
                        message.TaskStateKey.PartitionKey,
                        message.TaskStateKey.RowKey);
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount));
                }
                else
                {
                    _logger.LogError(
                        "Attempt {AttemptCount}: no task state for {StorageSuffix}, {PartitionKey}, {RowKey} was found. Giving up.",
                        message.AttemptCount,
                        message.TaskStateKey.StorageSuffix,
                        message.TaskStateKey.PartitionKey,
                        message.TaskStateKey.RowKey);
                }

                return;
            }

            var result = await _processor.ProcessAsync(message, taskState, dequeueCount);
            switch (result)
            {
                case TaskStateProcessResult.Complete:
                    await _taskStateStorageService.DeleteAsync(taskState);
                    break;
                case TaskStateProcessResult.Delay:
                    message.AttemptCount++;
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount));
                    break;
                case TaskStateProcessResult.Continue:
                    await _messageEnqueuer.EnqueueAsync(new[] { message });
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NuGet.Insights.Worker
{
    public class QueueStorageEnqueuer : IRawMessageEnqueuer
    {
        private readonly IWorkerQueueFactory _workerQueueFactory;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ILogger<QueueStorageEnqueuer> _logger;

        public QueueStorageEnqueuer(
            IWorkerQueueFactory workerQueueFactory,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger<QueueStorageEnqueuer> logger)
        {
            _workerQueueFactory = workerQueueFactory;
            _options = options;
            _logger = logger;
            BulkEnqueueStrategy = _options.Value.UseBulkEnqueueStrategy
                ? BulkEnqueueStrategy.Enabled(_options.Value.BulkEnqueueThreshold)
                : BulkEnqueueStrategy.Disabled();
        }

        public int MaxMessageSize => 65536;
        public BulkEnqueueStrategy BulkEnqueueStrategy { get; }

        public async Task InitializeAsync()
        {
            await _workerQueueFactory.InitializeAsync();
        }

        public async Task<int> GetApproximateMessageCountAsync(QueueType queue)
        {
            return await GetApproximateMessageCountAsync(await _workerQueueFactory.GetQueueAsync(queue));
        }

        public async Task<int> GetAvailableMessageCountLowerBoundAsync(QueueType queue, int messageCount)
        {
            return await GetAvailableMessageCountLowerBoundAsync(await _workerQueueFactory.GetQueueAsync(queue), messageCount);
        }

        public async Task<int> GetPoisonApproximateMessageCountAsync(QueueType queue)
        {
            return await GetApproximateMessageCountAsync(await _workerQueueFactory.GetPoisonQueueAsync(queue));
        }

        public async Task<int> GetPoisonAvailableMessageCountLowerBoundAsync(QueueType queue, int messageCount)
        {
            return await GetAvailableMessageCountLowerBoundAsync(await _workerQueueFactory.GetPoisonQueueAsync(queue), messageCount);
        }

        private static async Task<int> GetApproximateMessageCountAsync(QueueClient queue)
        {
            QueueProperties properties = await queue.GetPropertiesAsync();
            return properties.ApproximateMessagesCount;
        }

        private static async Task<int> GetAvailableMessageCountLowerBoundAsync(QueueClient queue, int messageCount)
        {
            PeekedMessage[] messages = await queue.PeekMessagesAsync(messageCount);
            return messages.Length;
        }

        public async Task ClearAsync(QueueType queue)
        {
            var queueClient = await _workerQueueFactory.GetQueueAsync(queue);
            await queueClient.ClearMessagesAsync();
        }

        public async Task ClearPoisonAsync(QueueType queue)
        {
            var queueClient = await _workerQueueFactory.GetPoisonQueueAsync(queue);
            await queueClient.ClearMessagesAsync();
        }

        public async Task AddAsync(QueueType queue, IReadOnlyList<string> messages)
        {
            await AddAsync(queue, messages, TimeSpan.Zero);
        }

        public async Task AddAsync(QueueType queue, IReadOnlyList<string> messages, TimeSpan visibilityDelay)
        {
            await AddAsync(queue, _workerQueueFactory.GetQueueAsync, messages, visibilityDelay);
        }

        public async Task AddPoisonAsync(QueueType queue, IReadOnlyList<string> messages)
        {
            await AddPoisonAsync(queue, messages, TimeSpan.Zero);
        }

        public async Task AddPoisonAsync(QueueType queue, IReadOnlyList<string> messages, TimeSpan visibilityDelay)
        {
            await AddAsync(queue, _workerQueueFactory.GetPoisonQueueAsync, messages, visibilityDelay);
        }

        private async Task AddAsync(QueueType queueType, Func<QueueType, Task<QueueClient>> getQueueAsync, IReadOnlyList<string> messages, TimeSpan visibilityDelay)
        {
            if (messages.Count == 0)
            {
                return;
            }

            var queue = await getQueueAsync(queueType);
            var workers = Math.Min(messages.Count, _options.Value.EnqueueWorkers);
            if (workers < 2)
            {
                _logger.LogInformation("Enqueueing {Count} individual messages to {QueueName}.", messages.Count, queue.Name);
                var completedCount = 0;
                foreach (var message in messages)
                {
                    await SendMessageAsync(queue, message, visibilityDelay);
                    completedCount++;
                    if (completedCount % 500 == 0 && completedCount < messages.Count)
                    {
                        _logger.LogInformation("Enqueued {CompletedCount} of {TotalCount} messages.", completedCount, messages.Count);
                    }
                }
                _logger.LogInformation("Done enqueueing {Count} individual messages to {QueueName}.", messages.Count, queue.Name);
            }
            else
            {
                var work = new ConcurrentQueue<string>(messages);

                _logger.LogInformation(
                    "Enqueueing {MessageCount} individual messages to {QueueName} with {WorkerCount} workers.",
                    messages.Count,
                    queue.Name,
                    workers);

                var tasks = Enumerable
                    .Range(0, workers)
                    .Select(async i =>
                    {
                        while (work.TryDequeue(out var message))
                        {
                            await SendMessageAsync(queue, message, visibilityDelay);
                        }
                    })
                    .ToList();

                await Task.WhenAll(tasks);

                _logger.LogInformation(
                    "Done enqueueing {MessageCount} individual messages to {QueueName} with {WorkerCount} workers.",
                    messages.Count,
                    queue.Name,
                    workers);
            }
        }

        private async Task SendMessageAsync(QueueClient queue, string message, TimeSpan visibilityTimeout)
        {
            await queue.SendMessageAsync(message, visibilityTimeout > TimeSpan.Zero && !_options.Value.DisableMessageDelay ? visibilityTimeout : null);
        }
    }
}

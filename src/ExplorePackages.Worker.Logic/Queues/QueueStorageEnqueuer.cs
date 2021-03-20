using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Worker
{
    public class QueueStorageEnqueuer : IRawMessageEnqueuer
    {
        private readonly IWorkerQueueFactory _workerQueueFactory;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;
        private readonly ILogger<QueueStorageEnqueuer> _logger;

        public QueueStorageEnqueuer(
            IWorkerQueueFactory workerQueueFactory,
            IOptions<ExplorePackagesWorkerSettings> options,
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

        public async Task<int> GetApproximateMessageCountAsync()
        {
            return await GetApproximateMessageCountAsync(await _workerQueueFactory.GetQueueAsync());
        }

        public async Task<int> GetAvailableMessageCountLowerBoundAsync(int messageCount)
        {
            return await GetAvailableMessageCountLowerBoundAsync(await _workerQueueFactory.GetQueueAsync(), messageCount);
        }

        public async Task<int> GetPoisonApproximateMessageCountAsync()
        {
            return await GetApproximateMessageCountAsync(await _workerQueueFactory.GetPoisonQueueAsync());
        }

        public async Task<int> GetPoisonAvailableMessageCountLowerBoundAsync(int messageCount)
        {
            return await GetAvailableMessageCountLowerBoundAsync(await _workerQueueFactory.GetPoisonQueueAsync(), messageCount);
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

        public async Task AddAsync(IReadOnlyList<string> messages)
        {
            await AddAsync(messages, TimeSpan.Zero);
        }

        public async Task AddAsync(IReadOnlyList<string> messages, TimeSpan visibilityDelay)
        {
            await AddAsync(_workerQueueFactory.GetQueueAsync, messages, visibilityDelay);
        }

        public async Task AddPoisonAsync(IReadOnlyList<string> messages)
        {
            await AddPoisonAsync(messages, TimeSpan.Zero);
        }

        public async Task AddPoisonAsync(IReadOnlyList<string> messages, TimeSpan visibilityDelay)
        {
            await AddAsync(_workerQueueFactory.GetPoisonQueueAsync, messages, visibilityDelay);
        }

        private async Task AddAsync(Func<Task<QueueClient>> getQueueAsync, IReadOnlyList<string> messages, TimeSpan visibilityDelay)
        {
            if (messages.Count == 0)
            {
                return;
            }

            var queue = await getQueueAsync();
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
            await queue.SendMessageAsync(message, visibilityTimeout > TimeSpan.Zero ? visibilityTimeout : null);
        }
    }
}

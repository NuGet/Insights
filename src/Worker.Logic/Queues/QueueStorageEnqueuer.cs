// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;

namespace NuGet.Insights.Worker
{
    public class QueueStorageEnqueuer : IRawMessageEnqueuer
    {
        private readonly IWorkerQueueFactory _workerQueueFactory;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ITelemetryClient _telemetryClient;
        private readonly ILogger<QueueStorageEnqueuer> _logger;
        private readonly IMetric _durationMs;
        private readonly IMetric _batchDurationMs;
        private readonly IMetric _averageDurationMs;
        private readonly IMetric _batchSizeMs;

        public QueueStorageEnqueuer(
            IWorkerQueueFactory workerQueueFactory,
            IOptions<NuGetInsightsWorkerSettings> options,
            ITelemetryClient telemetryClient,
            ILogger<QueueStorageEnqueuer> logger)
        {
            _workerQueueFactory = workerQueueFactory;
            _options = options;
            _telemetryClient = telemetryClient;
            _logger = logger;

            _durationMs = _telemetryClient.GetMetric("QueueStorageEnqueuer.SendMessageAsync.DurationMs", "QueueType", "IsPoison");
            _batchDurationMs = _telemetryClient.GetMetric("QueueStorageEnqueuer.BatchDurationMs", "QueueType", "IsPoison");
            _averageDurationMs = _telemetryClient.GetMetric("QueueStorageEnqueuer.AverageDurationMs", "QueueType", "IsPoison");
            _batchSizeMs = _telemetryClient.GetMetric("QueueStorageEnqueuer.BatchSize", "QueueType", "IsPoison");

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
            await AddAsync(queue, isPoison: false, _workerQueueFactory.GetQueueAsync, messages, visibilityDelay);
        }

        public async Task AddPoisonAsync(QueueType queue, IReadOnlyList<string> messages)
        {
            await AddPoisonAsync(queue, messages, TimeSpan.Zero);
        }

        public async Task AddPoisonAsync(QueueType queue, IReadOnlyList<string> messages, TimeSpan visibilityDelay)
        {
            await AddAsync(queue, isPoison: true, _workerQueueFactory.GetPoisonQueueAsync, messages, visibilityDelay);
        }

        private async Task AddAsync(QueueType queueType, bool isPoison, Func<QueueType, Task<QueueClient>> getQueueAsync, IReadOnlyList<string> messages, TimeSpan visibilityDelay)
        {
            if (messages.Count == 0)
            {
                return;
            }

            var queue = await getQueueAsync(queueType);
            var workers = Math.Min(messages.Count, _options.Value.EnqueueWorkers);

            var queueTypeString = queueType.ToString();
            var isPoisonString = isPoison.ToString();

            var sw = Stopwatch.StartNew();
            if (workers < 2)
            {
                _logger.LogInformation("Enqueueing {Count} individual messages to {QueueName}.", messages.Count, queue.Name);
                var completedCount = 0;
                foreach (var message in messages)
                {
                    await SendMessageAsync(queueTypeString, isPoisonString, queue, message, visibilityDelay);
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
                            await SendMessageAsync(queueTypeString, isPoisonString, queue, message, visibilityDelay);
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
            sw.Stop();

            _batchDurationMs.TrackValue(sw.Elapsed.TotalMilliseconds, queueTypeString, isPoisonString);
            _averageDurationMs.TrackValue(sw.Elapsed.TotalMilliseconds / messages.Count, queueTypeString, isPoisonString);
            _batchSizeMs.TrackValue(messages.Count, queueTypeString, isPoisonString);
        }

        private async Task SendMessageAsync(string queueTypeString, string isPoisonString, QueueClient queue, string message, TimeSpan visibilityTimeout)
        {
            var sw = Stopwatch.StartNew();
            await queue.SendMessageAsync(message, visibilityTimeout > TimeSpan.Zero && !_options.Value.DisableMessageDelay ? visibilityTimeout : null);
            sw.Stop();
            _durationMs.TrackValue(sw.Elapsed.TotalMilliseconds, queueTypeString, isPoisonString);
        }
    }
}

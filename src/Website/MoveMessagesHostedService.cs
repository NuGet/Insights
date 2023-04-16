// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NuGet.Insights.Worker;

namespace NuGet.Insights.Website
{
    public class MoveMessagesHostedService : BackgroundService
    {
        private readonly MoveMessagesTaskQueue _taskQueue;
        private readonly AutoRenewingStorageLeaseService _leaseService;
        private readonly SchemaSerializer _serializer;
        private readonly SchemaCollection _schemaCollection;
        private readonly IWorkerQueueFactory _workerQueueFactory;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly ITelemetryClient _telemetryClient;
        private readonly ILogger<MoveMessagesHostedService> _logger;

        public MoveMessagesHostedService(
            MoveMessagesTaskQueue taskQueue,
            AutoRenewingStorageLeaseService leaseService,
            SchemaSerializer serializer,
            SchemaCollection schemaCollection,
            IWorkerQueueFactory workerQueueFactory,
            IMessageEnqueuer messageEnqueuer,
            ITelemetryClient telemetryClient,
            ILogger<MoveMessagesHostedService> logger)
        {
            _taskQueue = taskQueue;
            _leaseService = leaseService;
            _serializer = serializer;
            _schemaCollection = schemaCollection;
            _workerQueueFactory = workerQueueFactory;
            _messageEnqueuer = messageEnqueuer;
            _telemetryClient = telemetryClient;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var moveTask = await _taskQueue.DequeueAsync(stoppingToken);
                try
                {
                    await ExecuteMoveTaskAsync(moveTask, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(
                        ex,
                        "An error was encountered while processing a message move operation from {Source} ({SourceMainOrPoison}) to {Destination} ({DestinationMainOrPoison}).",
                        moveTask.Source,
                        moveTask.IsPoisonSource ? "poison" : "main",
                        moveTask.Destination,
                        moveTask.IsPoisonDestination ? "poison" : "main");
                }
                finally
                {
                    _taskQueue.TryMarkComplete(moveTask);
                }
            }
        }

        private async Task ExecuteMoveTaskAsync(MoveMessagesTask moveTask, CancellationToken stoppingToken)
        {
            (var source, var isPoisonSource, var destination, var isPoisonDestination) = moveTask;

            var metricProperties = new Dictionary<string, string>
            {
                { "Source", source.ToString() },
                { "IsPoisonSource", isPoisonSource ? "true" : "false" },
                { "Destination", destination.ToString() },
                { "IsPoisonDestination", isPoisonDestination ? "true" : "false" },
            };

            _logger.LogInformation(
                "Starting message move operation from {Source} ({SourceMainOrPoison}) to {Destination} ({DestinationMainOrPoison}).",
                source,
                isPoisonSource ? "poison" : "main",
                destination,
                isPoisonDestination ? "poison" : "main");
            var startTime = DateTime.UtcNow;

            var leaseName = $"{nameof(MoveMessagesTask)}" +
                $"-{source}-{(isPoisonSource ? "poison" : "main")}" +
                $"-{destination}-{(isPoisonDestination ? "poison" : "main")}";

            await using var lease = await _leaseService.TryAcquireAsync(leaseName);
            if (!lease.Acquired)
            {
                _logger.LogTransientWarning("Lease {LeaseName} is not available. Skipping this move task.", leaseName);
                return;
            }

            stoppingToken.ThrowIfCancellationRequested();
            var sourceQueue = await GetQueueAsync(source, isPoisonSource);
            var destinationQueue = await GetQueueAsync(source, isPoisonDestination);

            int messageCount;
            var messagesIds = new HashSet<string>();
            using var loopMetrics = _telemetryClient.StartQueryLoopMetrics(
                dimension1Name: "Source",
                dimension1Value: metricProperties["Source"],
                dimension2Name: "IsPoisonSource",
                dimension2Value: metricProperties["IsPoisonSource"],
                dimension3Name: "Destination",
                dimension3Value: metricProperties["Destination"],
                dimension4Name: "IsPoisonDestination",
                dimension4Value: metricProperties["IsPoisonDestination"]);
            do
            {
                QueueMessage[] messages;
                var batchSw = Stopwatch.StartNew();
                using (var queryMetrics = loopMetrics.TrackQuery())
                {
                    messages = await sourceQueue.ReceiveMessagesAsync(
                        maxMessages: StorageUtility.MaxDequeueCount,
                        visibilityTimeout: TimeSpan.FromMinutes(1),
                        cancellationToken: stoppingToken);

                }

                messageCount = messages.Length;
                _logger.LogInformation("Fetched {Count} messages from queue {QueueName}.", messageCount, sourceQueue.Name);

                var skip = new Stack<QueueMessage>();
                var bulkMove = new List<(QueueMessage Raw, JsonElement Serialized)>();
                var individualMove = new List<(QueueMessage Raw, Type Type, string Serialized)>();
                foreach (var raw in messages)
                {
                    if (!messagesIds.Add(raw.MessageId))
                    {
                        skip.Push(raw);
                        continue;
                    }

                    if (raw.InsertedOn.HasValue && raw.InsertedOn > startTime)
                    {
                        skip.Push(raw);
                        continue;
                    }

                    stoppingToken.ThrowIfCancellationRequested();

                    try
                    {
                        var deserialized = _serializer.Deserialize(raw.Body.ToMemory());
                        var dataType = deserialized.Data.GetType();
                        var serializer = _serializer.GetGenericSerializer(dataType);
                        var serialized = serializer.SerializeMessage(deserialized.Data);

                        if (dataType == typeof(HeterogeneousBulkEnqueueMessage)
                            || dataType == typeof(HomogeneousBulkEnqueueMessage))
                        {
                            individualMove.Add((raw, dataType, serialized.AsString()));
                        }
                        else
                        {
                            bulkMove.Add((raw, serialized.AsJsonElement()));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize message {MessageId}. Skipping.", raw.MessageId);
                        skip.Push(raw);
                        continue;
                    }
                }

                messageCount -= skip.Count;

                while (skip.Count > 0)
                {
                    var raw = skip.Pop();
                    try
                    {
                        await sourceQueue.UpdateMessageAsync(
                            raw.MessageId,
                            raw.PopReceipt,
                            visibilityTimeout: TimeSpan.Zero,
                            cancellationToken: stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to return message {MessageId} to the queue. Skipping.", raw.MessageId);
                    }
                }

                if (bulkMove.Count > 0)
                {
                    var bulkMessage = new HeterogeneousBulkEnqueueMessage
                    {
                        Messages = bulkMove.Select(x => x.Serialized).ToList(),
                    };
                    await _messageEnqueuer.EnqueueAsync(destination, isPoisonDestination, new[] { bulkMessage }, Split, TimeSpan.Zero);
                    _logger.LogInformation("Enqueued new heterogeneous bulk enqueue message(s) to queue {QueueName}.", destinationQueue.Name);

                    stoppingToken.ThrowIfCancellationRequested();

                    foreach (var message in bulkMove)
                    {
                        try
                        {
                            await sourceQueue.DeleteMessageAsync(
                                message.Raw.MessageId,
                                message.Raw.PopReceipt,
                                cancellationToken: stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to delete message {MessageId}. Skipping.", message.Raw.MessageId);
                        }
                    }
                }

                if (individualMove.Count > 0)
                {
                    foreach (var message in individualMove)
                    {
                        await destinationQueue.SendMessageAsync(message.Serialized, stoppingToken);
                        _logger.LogInformation("Enqueued {MessageType} individual message to queue {QueueName}.", message.Type, destinationQueue.Name);

                        stoppingToken.ThrowIfCancellationRequested();

                        try
                        {
                            await sourceQueue.DeleteMessageAsync(
                                message.Raw.MessageId,
                                message.Raw.PopReceipt,
                                cancellationToken: stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to delete message {MessageId}. Skipping.", message.Raw.MessageId);
                        }
                    }
                }

                _telemetryClient.TrackMetric(
                    nameof(MoveMessagesHostedService) + ".MoveBatch.ElapsedMs",
                    batchSw.Elapsed.TotalMilliseconds,
                    metricProperties);
            }
            while (messageCount > 0 && !stoppingToken.IsCancellationRequested);
        }

        private IReadOnlyList<HeterogeneousBulkEnqueueMessage> Split(HeterogeneousBulkEnqueueMessage message)
        {
            if (message.Messages.Count <= 2)
            {
                return null;
            }

            var firstHalf = message.Messages.Take(message.Messages.Count / 2).ToList();
            var secondHalf = message.Messages.Skip(firstHalf.Count).ToList();

            return new[]
            {
                new HeterogeneousBulkEnqueueMessage { Messages = firstHalf },
                new HeterogeneousBulkEnqueueMessage { Messages = secondHalf },
            };
        }

        private async Task<QueueClient> GetQueueAsync(QueueType type, bool isPoison)
        {
            return isPoison ? await _workerQueueFactory.GetPoisonQueueAsync(type) : await _workerQueueFactory.GetQueueAsync(type);
        }
    }
}

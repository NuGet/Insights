// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NuGet.Insights.Worker;

namespace NuGet.Insights.Tool
{
    public class ProcessMessagesCommand : ICommand
    {
        private readonly IWorkerQueueFactory _workerQueueFactory;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ProcessMessagesCommand> _logger;
        private CommandOption<int> _workerCount;

        public ProcessMessagesCommand(
            IWorkerQueueFactory workerQueueFactory,
            IServiceProvider serviceProvider,
            ILogger<ProcessMessagesCommand> logger)
        {
            _workerQueueFactory = workerQueueFactory;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public void Configure(CommandLineApplication app)
        {
            _workerCount = app.Option<int>(
                "--worker-count",
                "number of worker tasks processing messages in parallel",
                CommandOptionType.SingleValue);
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            int workerCount = _workerCount.HasValue() ? _workerCount.ParsedValue : 1;

            await _workerQueueFactory.InitializeAsync();
            var expandQueue = await _workerQueueFactory.GetQueueAsync(QueueType.Expand);
            var workerQueue = await _workerQueueFactory.GetQueueAsync(QueueType.Work);

            async Task<(QueueType queueType, QueueClient queue, QueueMessage message)> ReceiveMessageAsync()
            {
                QueueMessage message = await expandQueue.ReceiveMessageAsync();
                if (message != null)
                {
                    return (QueueType.Expand, expandQueue, message);
                }

                message = await workerQueue.ReceiveMessageAsync();
                if (message != null)
                {
                    return (QueueType.Work, workerQueue, message);
                }

                return (QueueType.Work, null, null);
            };

            _logger.LogInformation("Starting to process messages with {Count} workers.", workerCount);

            await Task.WhenAll(Enumerable
                .Range(0, workerCount)
                .Select(async workerIndex =>
                {
                    while (!token.IsCancellationRequested)
                    {
                        (var queueType, var queue, var message) = await ReceiveMessageAsync();
                        if (message != null)
                        {
                            _logger.LogInformation("[Worker {WorkerIndex}] Message found. Processing.", workerIndex);
                            using (var scope = _serviceProvider.CreateScope())
                            {
                                await ProcessMessageAsync(scope.ServiceProvider, queueType, message);
                            }

                            await queue.DeleteMessageAsync(message.MessageId, message.PopReceipt);
                        }
                        else
                        {
                            _logger.LogInformation("[Worker {WorkerIndex}] No messages found. Waiting.", workerIndex);
                            await Task.Delay(TimeSpan.FromSeconds(5), token);
                        }
                    }

                    _logger.LogInformation("[Worker {WorkerIndex}] Stopping.", workerIndex);
                }));
        }

        private async Task ProcessMessageAsync(IServiceProvider serviceProvider, QueueType queue, QueueMessage message)
        {
            var leaseScope = serviceProvider.GetRequiredService<TempStreamLeaseScope>();
            await using var scopeOwnership = leaseScope.TakeOwnership();
            var messageProcessor = serviceProvider.GetRequiredService<IGenericMessageProcessor>();
            await messageProcessor.ProcessSingleAsync(queue, message.Body.ToMemory(), message.DequeueCount);
        }
    }
}

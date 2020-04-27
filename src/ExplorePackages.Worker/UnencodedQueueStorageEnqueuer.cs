using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage.Queue;

namespace Knapcode.ExplorePackages.Worker
{

    public class UnencodedQueueStorageEnqueuer : IRawMessageEnqueuer
    {
        private readonly IOptionsSnapshot<QueueStorageEnqueuerConfiguration> _options;
        private readonly ILogger<UnencodedQueueStorageEnqueuer> _logger;

        private CloudQueue _target;

        public UnencodedQueueStorageEnqueuer(
            IOptionsSnapshot<QueueStorageEnqueuerConfiguration> options,
            ILogger<UnencodedQueueStorageEnqueuer> logger)
        {
            _options = options;
            _logger = logger;
            BulkEnqueueStrategy = _options.Value.UseBulkEnqueueStrategy
                ? BulkEnqueueStrategy.Enabled(_options.Value.BulkEnqueueThreshold, maxSize: 65536)
                : BulkEnqueueStrategy.Disabled();
        }

        public BulkEnqueueStrategy BulkEnqueueStrategy { get; }

        public void SetTarget(CloudQueue target)
        {
            target.EncodeMessage = false;

            var output = Interlocked.CompareExchange(ref _target, target, null);
            if (output != null)
            {
                throw new InvalidOperationException("The target has already been set.");
            }
        }

        public async Task AddAsync(IReadOnlyList<string> messages)
        {
            if (_target == null)
            {
                throw new InvalidOperationException("The target has not been set.");
            }

            if (messages.Count == 0)
            {
                return;
            }

            var workers = Math.Min(messages.Count, _options.Value.EnqueueWorkers);
            if (workers < 2)
            {
                _logger.LogInformation("Enqueueing {Count} individual messages.", messages.Count);
                foreach (var message in messages)
                {
                    await AddMessageAsync(message);
                }
            }
            else
            {
                var work = new ConcurrentQueue<string>(messages);

                _logger.LogInformation(
                    "Enqueueing {MessageCount} individual messages with {WorkerCount} workers.",
                    messages.Count,
                    workers);

                var tasks = Enumerable
                    .Range(0, workers)
                    .Select(async i =>
                    {
                        while (work.TryDequeue(out var message))
                        {
                            await AddMessageAsync(message);
                        }
                    })
                    .ToList();

                await Task.WhenAll(tasks);
            }
        }

        private async Task AddMessageAsync(string message)
        {
            await _target.AddMessageAsync(new CloudQueueMessage(message));
        }
    }
}

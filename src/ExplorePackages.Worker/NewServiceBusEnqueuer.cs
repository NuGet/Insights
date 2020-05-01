using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Knapcode.ExplorePackages.Logic.Worker;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Worker
{
    public class NewServiceBusEnqueuer : IRawMessageEnqueuer
    {
        private readonly ServiceBusSender _target;
        private readonly ILogger<NewServiceBusEnqueuer> _logger;

        public NewServiceBusEnqueuer(ServiceBusSender target, ILogger<NewServiceBusEnqueuer> logger)
        {
            _target = target;
            _logger = logger;
        }

        public BulkEnqueueStrategy BulkEnqueueStrategy { get; } = BulkEnqueueStrategy.Disabled();

        public async Task AddAsync(IReadOnlyList<string> messages)
        {
            await AddAsync(messages, TimeSpan.Zero);
        }

        public async Task AddAsync(IReadOnlyList<string> messages, TimeSpan notBefore)
        {
            if (messages.Count == 0)
            {
                return;
            }

            DateTimeOffset? scheduledEnqueueTime = null;
            if (notBefore > TimeSpan.Zero)
            {
                scheduledEnqueueTime = DateTimeOffset.UtcNow + notBefore;
            }
            
            if (messages.Count == 1)
            {
                _logger.LogInformation("Enqueueing a single message.");
                await _target.SendAsync(GetEncodedMessage(messages.Single(), scheduledEnqueueTime));
            }
            else
            {
                var batch = await _target.CreateBatchAsync();
                foreach (string message in messages)
                {
                    if (!batch.TryAdd(GetEncodedMessage(message, scheduledEnqueueTime)))
                    {
                        if (batch.Count == 0)
                        {
                            throw new InvalidOperationException("A single message is too large.");
                        }

                        await SendBatchAsync(batch);
                        batch = await _target.CreateBatchAsync();
                    }
                }

                if (batch.Count > 0)
                {
                    await SendBatchAsync(batch);
                }
            }
        }

        private async Task SendBatchAsync(ServiceBusMessageBatch batch)
        {
            _logger.LogInformation("Enqueueing batch of {Count} messages.", batch.Count);
            await _target.SendBatchAsync(batch);
        }

        private static ServiceBusMessage GetEncodedMessage(string message, DateTimeOffset? scheduledEnqueueTime)
        {
            var encodedMessage = new ServiceBusMessage(Encoding.UTF8.GetBytes(message));

            if (scheduledEnqueueTime.HasValue)
            {
                encodedMessage.ScheduledEnqueueTime = scheduledEnqueueTime.Value;
            }

            return encodedMessage;
        }
    }
}

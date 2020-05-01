using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic.Worker;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Worker
{
    public class OldServiceBusEnqueuer : IRawMessageEnqueuer
    {
        /// <summary>
        /// Leave some percentage for overhead.
        /// </summary>
        private const int ServiceBusMaxMessageSize = (int)(256 * 1024 * 0.70);

        private ISenderClient _target;
        private readonly ILogger<OldServiceBusEnqueuer> _logger;

        public OldServiceBusEnqueuer(ILogger<OldServiceBusEnqueuer> logger)
        {
            _logger = logger;
        }

        public BulkEnqueueStrategy BulkEnqueueStrategy { get; } = BulkEnqueueStrategy.Disabled();

        public void SetTarget(ISenderClient target)
        {
            var output = Interlocked.CompareExchange(ref _target, target, null);
            if (output != null)
            {
                throw new InvalidOperationException("The target has already been set.");
            }
        }

        public async Task AddAsync(IReadOnlyList<string> messages)
        {
            await AddAsync(messages, TimeSpan.Zero);
        }

        public async Task AddAsync(IReadOnlyList<string> messages, TimeSpan notBefore)
        {
            if (_target == null)
            {
                throw new InvalidOperationException("The target has not been set.");
            }

            DateTime? scheduledEnqueueTimeUtc = null;
            if (notBefore > TimeSpan.Zero)
            {
                scheduledEnqueueTimeUtc = DateTime.UtcNow + notBefore;
            }

            if (messages.Count == 0)
            {
                return;
            }

            if (messages.Count == 1)
            {
                _logger.LogInformation("Enqueueing a single message.");
                await _target.SendAsync(GetEncodedMessage(messages.Single(), scheduledEnqueueTimeUtc));
            }
            else
            {
                var batch = new List<Message>();
                var batchSize = 0;

                foreach (string message in messages)
                {
                    var encodedMessage = GetEncodedMessage(message, scheduledEnqueueTimeUtc);
                    var encodedMessageSize = encodedMessage.Body.Length;

                    if (batchSize + encodedMessageSize > ServiceBusMaxMessageSize)
                    {
                        if (encodedMessageSize > ServiceBusMaxMessageSize)
                        {
                            throw new InvalidOperationException("A single message is too large.");
                        }

                        await SendBatchAsync(batch);
                        batch = new List<Message>();
                        batchSize = 0;
                    }

                    batch.Add(encodedMessage);
                    batchSize += encodedMessage.Body.Length;
                }

                if (batch.Count > 0)
                {
                    await SendBatchAsync(batch);
                }
            }
        }

        private async Task SendBatchAsync(List<Message> batch)
        {
            _logger.LogInformation("Enqueueing batch of {Count} messages.", batch.Count);
            await _target.SendAsync(batch);
        }

        private static Message GetEncodedMessage(string message, DateTime? scheduledEnqueueTimeUtc)
        {
            var encodedMessage = new Message(Encoding.UTF8.GetBytes(message));

            if (scheduledEnqueueTimeUtc.HasValue)
            {
                encodedMessage.ScheduledEnqueueTimeUtc = scheduledEnqueueTimeUtc.Value;
            }

            return encodedMessage;
        }
    }
}

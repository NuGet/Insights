using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Worker
{
    public class MixedBulkEnqueueMessageProcessor : IMessageProcessor<MixedBulkEnqueueMessage>
    {
        private readonly IRawMessageEnqueuer _messageEnqueuer;
        private readonly ILogger<MixedBulkEnqueueMessage> _logger;

        public MixedBulkEnqueueMessageProcessor(IRawMessageEnqueuer messageEnqueuer, ILogger<MixedBulkEnqueueMessage> logger)
        {
            _messageEnqueuer = messageEnqueuer;
            _logger = logger;
        }

        public async Task ProcessAsync(MixedBulkEnqueueMessage message, long dequeueCount)
        {
            _logger.LogInformation("Processing mixed bulk enqueue message with {Count} messages.", message.Messages.Count);

            var messages = message.Messages.Select(x => new SerializedMessage(() => x).AsString()).ToList();
            await _messageEnqueuer.AddAsync(messages, message.NotBefore.GetValueOrDefault(TimeSpan.Zero));
        }
    }
}

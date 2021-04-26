using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Worker
{
    public class HomogeneousBulkEnqueueMessageProcessor : IMessageProcessor<HomogeneousBulkEnqueueMessage>
    {
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly IRawMessageEnqueuer _rawMessageEnqueuer;
        private readonly ILogger<HomogeneousBulkEnqueueMessageProcessor> _logger;

        public HomogeneousBulkEnqueueMessageProcessor(
            IMessageEnqueuer messageEnqueuer,
            IRawMessageEnqueuer rawMessageEnqueuer,
            ILogger<HomogeneousBulkEnqueueMessageProcessor> logger)
        {
            _messageEnqueuer = messageEnqueuer;
            _rawMessageEnqueuer = rawMessageEnqueuer;
            _logger = logger;
        }

        public async Task ProcessAsync(HomogeneousBulkEnqueueMessage message, long dequeueCount)
        {
            _logger.LogInformation("Processing homogeneous bulk enqueue message with {Count} messages.", message.Messages.Count);

            var messages = message
                .Messages
                .Select(x => NameVersionSerializer.SerializeMessage(message.SchemaName, message.SchemaVersion, x).AsString())
                .ToList();

            await _rawMessageEnqueuer.AddAsync(
                _messageEnqueuer.GetQueueType(message.SchemaName),
                messages,
                message.NotBefore.GetValueOrDefault(TimeSpan.Zero));
        }
    }
}

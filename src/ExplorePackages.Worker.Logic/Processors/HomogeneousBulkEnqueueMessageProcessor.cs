using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Worker
{
    public class HomogeneousBulkEnqueueMessageProcessor : IMessageProcessor<HomogeneousBulkEnqueueMessage>
    {
        private readonly IRawMessageEnqueuer _messageEnqueuer;
        private readonly ILogger<HomogeneousBulkEnqueueMessageProcessor> _logger;

        public HomogeneousBulkEnqueueMessageProcessor(IRawMessageEnqueuer messageEnqueuer, ILogger<HomogeneousBulkEnqueueMessageProcessor> logger)
        {
            _messageEnqueuer = messageEnqueuer;
            _logger = logger;
        }

        public async Task ProcessAsync(HomogeneousBulkEnqueueMessage message)
        {
            _logger.LogInformation("Processing homogeneous bulk enqueue message with {Count} messages.", message.Messages.Count);

            var messages = message
                .Messages
                .Select(x => NameVersionSerializer.SerializeMessage(message.SchemaName, message.SchemaVersion, x).AsString())
                .ToList();

            await _messageEnqueuer.AddAsync(messages, message.NotBefore);
        }
    }
}

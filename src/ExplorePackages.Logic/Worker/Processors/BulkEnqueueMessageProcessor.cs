using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class BulkEnqueueMessageProcessor : IMessageProcessor<BulkEnqueueMessage>
    {
        private readonly IRawMessageEnqueuer _messageEnqueuer;
        private readonly ILogger<BulkEnqueueMessage> _logger;

        public BulkEnqueueMessageProcessor(IRawMessageEnqueuer messageEnqueuer, ILogger<BulkEnqueueMessage> logger)
        {
            _messageEnqueuer = messageEnqueuer;
            _logger = logger;
        }

        public async Task ProcessAsync(BulkEnqueueMessage message)
        {
            _logger.LogInformation("Processing bulk enqueue message with {Count} messages.", message.Messages.Count);

            var messages = message.Messages.Select(x => new SerializedMessage(() => x).AsString()).ToList();
            await _messageEnqueuer.AddAsync(messages, message.NotBefore);
        }
    }
}

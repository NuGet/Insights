using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Worker
{
    public class HomogeneousBatchMessageProcessor : IMessageProcessor<HomogeneousBatchMessage>
    {
        private readonly IGenericMessageProcessor _messageProcessor;
        private readonly IRawMessageEnqueuer _messageEnqueuer;
        private readonly ILogger<HomogeneousBatchMessageProcessor> _logger;

        public HomogeneousBatchMessageProcessor(
            IGenericMessageProcessor messageProcessor,
            IRawMessageEnqueuer messageEnqueuer,
            ILogger<HomogeneousBatchMessageProcessor> logger)
        {
            _messageProcessor = messageProcessor;
            _messageEnqueuer = messageEnqueuer;
            _logger = logger;
        }

        public async Task ProcessAsync(HomogeneousBatchMessage batch, long dequeueCount)
        {
            using (_logger.BeginScope("Processing homogeneous batch message with {Scope_Count} messages", batch.Messages.Count))
            {
                await _messageProcessor.ProcessBatchAsync(
                    batch.SchemaName,
                    batch.SchemaVersion,
                    batch.Messages,
                    dequeueCount);
            }
        }
    }
}

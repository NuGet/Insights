using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Insights.Worker
{
    public class HomogeneousBatchMessageProcessor : IMessageProcessor<HomogeneousBatchMessage>
    {
        private readonly IGenericMessageProcessor _messageProcessor;
        private readonly ILogger<HomogeneousBatchMessageProcessor> _logger;

        public HomogeneousBatchMessageProcessor(
            IGenericMessageProcessor messageProcessor,
            ILogger<HomogeneousBatchMessageProcessor> logger)
        {
            _messageProcessor = messageProcessor;
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

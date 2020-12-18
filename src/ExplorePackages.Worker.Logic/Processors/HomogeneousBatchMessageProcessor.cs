using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Worker
{
    public class HomogeneousBatchMessageProcessor : IMessageProcessor<HomogeneousBatchMessage>
    {
        private readonly GenericMessageProcessor _messageProcessor;
        private readonly ILogger<HomogeneousBatchMessageProcessor> _logger;

        public HomogeneousBatchMessageProcessor(
            GenericMessageProcessor messageProcessor,
            ILogger<HomogeneousBatchMessageProcessor> logger)
        {
            _messageProcessor = messageProcessor;
            _logger = logger;
        }

        public async Task ProcessAsync(HomogeneousBatchMessage batchMessage)
        {
            _logger.LogInformation("Processing homogeneous batch message with {Count} messages.", batchMessage.Messages.Count);

            foreach (var data in batchMessage.Messages)
            {
                var singleMessage = new NameVersionMessage<JToken>(batchMessage.SchemaName, batchMessage.SchemaVersion, data);
                await _messageProcessor.ProcessAsync(singleMessage);
            }
        }
    }
}

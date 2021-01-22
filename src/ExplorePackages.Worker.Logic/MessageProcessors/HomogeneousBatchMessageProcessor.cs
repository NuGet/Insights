using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

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

        public async Task ProcessAsync(HomogeneousBatchMessage batch, int dequeueCount)
        {
            using (_logger.BeginScope("Processing homogeneous batch message with {Scope_Count} messages", batch.Messages.Count))
            {
                if (dequeueCount > 1)
                {
                    _logger.LogWarning("Homogeneous batch message has been attempted multiple times. Retrying messages individually.");

                    await _messageEnqueuer.AddAsync(SerializeMessages(batch, batch.Messages));
                }
                else
                {
                    _logger.LogInformation("Processing homogeneous batch message with {Count} messages.", batch.Messages.Count);

                    var result = await _messageProcessor.ProcessAsync(batch.SchemaName, batch.SchemaVersion, batch.Messages, dequeueCount);

                    if (result.Failed.Any())
                    {
                        _logger.LogError("{ErrorCount} messages in a batch of {Count} failed. Retrying messages individually.", result.Failed.Count, batch.Messages.Count);
                        await _messageEnqueuer.AddAsync(SerializeMessages(batch, result.Failed));
                    }

                    if (result.TryAgainLater.Any())
                    {
                        foreach (var (notBefore, messages) in result.TryAgainLater.OrderBy(x => x.Key))
                        {
                            if (messages.Any())
                            {
                                _logger.LogInformation("{TryAgainLaterCount} messages in a batch of {Count} need to be tried again. Retrying messages individually.", messages.Count, batch.Messages.Count);
                                await _messageEnqueuer.AddAsync(SerializeMessages(batch, messages), notBefore);
                            }
                        }
                    }
                }
            }
        }

        private static List<string> SerializeMessages(HomogeneousBatchMessage batch, IReadOnlyList<JToken> messages)
        {
            return messages.Select(x => SerializeMessage(batch, x)).ToList();
        }

        private static string SerializeMessage(HomogeneousBatchMessage batch, JToken data)
        {
            return NameVersionSerializer.SerializeMessage(
                batch.SchemaName,
                batch.SchemaVersion,
                data).AsString();
        }
    }
}

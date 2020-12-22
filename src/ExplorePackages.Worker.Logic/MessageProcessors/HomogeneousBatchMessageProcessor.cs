using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Worker
{
    public class HomogeneousBatchMessageProcessor : IMessageProcessor<HomogeneousBatchMessage>
    {
        private readonly GenericMessageProcessor _messageProcessor;
        private readonly IRawMessageEnqueuer _messageEnqueuer;
        private readonly ILogger<HomogeneousBatchMessageProcessor> _logger;

        public HomogeneousBatchMessageProcessor(
            GenericMessageProcessor messageProcessor,
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
                    var messages = new List<string>();
                    foreach (var data in batch.Messages)
                    {
                        messages.Add(GetSerializedMessage(batch, data));
                    }

                    _logger.LogWarning("Homogeneous batch message has been attempted multiple times. Retrying messages individually.");
                    await _messageEnqueuer.AddAsync(messages);
                }
                else
                {
                    _logger.LogInformation("Processing homogeneous batch message with {Count} messages.", batch.Messages.Count);

                    var failed = new List<string>();
                    foreach (var data in batch.Messages)
                    {
                        var singleMessage = new NameVersionMessage<JToken>(batch.SchemaName, batch.SchemaVersion, data);
                        try
                        {
                            await _messageProcessor.ProcessAsync(singleMessage, dequeueCount);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "A message in a batch failed.");
                            failed.Add(GetSerializedMessage(batch, data));
                        }
                    }

                    if (failed.Any())
                    {
                        _logger.LogError("{FailedCount} messages in a batch of {Count} failed. Retrying messages individually.", failed.Count, batch.Messages.Count);
                        await _messageEnqueuer.AddAsync(failed);
                    }
                }
            }
        }

        private static string GetSerializedMessage(HomogeneousBatchMessage batch, JToken data)
        {
            return NameVersionSerializer.SerializeMessage(
                batch.SchemaName,
                batch.SchemaVersion,
                data).AsString();
        }
    }
}

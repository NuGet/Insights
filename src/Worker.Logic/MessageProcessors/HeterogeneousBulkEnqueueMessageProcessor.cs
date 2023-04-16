// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Insights.Worker
{
    public class HeterogeneousBulkEnqueueMessageProcessor : IMessageProcessor<HeterogeneousBulkEnqueueMessage>
    {
        private readonly SchemaSerializer _serializer;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly IRawMessageEnqueuer _rawMessageEnqueuer;
        private readonly ILogger<HeterogeneousBulkEnqueueMessageProcessor> _logger;

        public HeterogeneousBulkEnqueueMessageProcessor(
            SchemaSerializer serializer,
            IMessageEnqueuer messageEnqueuer,
            IRawMessageEnqueuer rawMessageEnqueuer,
            ILogger<HeterogeneousBulkEnqueueMessageProcessor> logger)
        {
            _serializer = serializer;
            _messageEnqueuer = messageEnqueuer;
            _rawMessageEnqueuer = rawMessageEnqueuer;
            _logger = logger;
        }

        public async Task ProcessAsync(HeterogeneousBulkEnqueueMessage message, long dequeueCount)
        {
            _logger.LogInformation("Processing heterogenous bulk enqueue message with {Count} messages.", message.Messages.Count);

            var deserializedMessages = message.Messages.Select(_serializer.Deserialize).ToList();

            foreach (var group in deserializedMessages.GroupBy(x => _messageEnqueuer.GetQueueType(x.SchemaName)))
            {
                var messages = group
                    .Select(x => NameVersionSerializer.SerializeData(x).AsString())
                    .ToList();
                await _rawMessageEnqueuer.AddAsync(group.Key, messages);
            }
        }
    }
}

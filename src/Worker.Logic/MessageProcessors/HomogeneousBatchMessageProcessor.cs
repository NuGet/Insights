// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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

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
            using var loggerScope = _logger.BeginScope("Homogenous batch message: {Scope_HomogeneousBatchCount}x {Scope_HomogeneousBatchSchemaName}", batch.Messages.Count, batch.SchemaName);

            await _messageProcessor.ProcessBatchAsync(
                batch.SchemaName,
                batch.SchemaVersion,
                batch.Messages,
                dequeueCount);
        }
    }
}

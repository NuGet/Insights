// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Queues;

namespace NuGet.Insights.Worker
{
    public class UnencodedQueueProcessorFactory : IQueueProcessorFactory
    {
        public QueueProcessor Create(QueueProcessorFactoryContext context)
        {
            context.Queue.EncodeMessage = false;
            if (context.PoisonQueue != null)
            {
                context.PoisonQueue.EncodeMessage = false;
            }

            return new QueueProcessor(context);
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Storage.Queues;

namespace NuGet.Insights.Worker
{
    public class WorkerQueueFactory : IWorkerQueueFactory
    {
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public WorkerQueueFactory(
            ServiceClientFactory serviceClientFactory,
            IOptions<NuGetInsightsWorkerSettings> options)
        {
            _serviceClientFactory = serviceClientFactory;
            _options = options;
        }

        public async Task InitializeAsync()
        {
            foreach (var type in Enum.GetValues(typeof(QueueType)).Cast<QueueType>())
            {
                await (await GetQueueAsync(type)).CreateIfNotExistsAsync(retry: true);
                await (await GetPoisonQueueAsync(type)).CreateIfNotExistsAsync(retry: true);
            }
        }

        public async Task<QueueClient> GetQueueAsync(QueueType type)
        {
            return (await _serviceClientFactory.GetQueueServiceClientAsync())
                .GetQueueClient(GetQueueName(type));
        }

        public async Task<QueueClient> GetPoisonQueueAsync(QueueType type)
        {
            return (await _serviceClientFactory.GetQueueServiceClientAsync())
                .GetQueueClient(GetQueueName(type) + "-poison");
        }

        private string GetQueueName(QueueType type)
        {
            switch (type)
            {
                case QueueType.Work:
                    return _options.Value.WorkQueueName;
                case QueueType.Expand:
                    return _options.Value.ExpandQueueName;
                default:
                    throw new NotImplementedException();
            }
        }
    }
}

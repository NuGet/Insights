// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Storage.Queues;

namespace NuGet.Insights.Worker
{
    public class WorkerQueueFactory : IWorkerQueueFactory
    {
        private readonly ContainerInitializationState _workInitializationState;
        private readonly ContainerInitializationState _workPoisonInitializationState;
        private readonly ContainerInitializationState _expandInitializationState;
        private readonly ContainerInitializationState _expandPoisonInitializationState;

        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public WorkerQueueFactory(
            ServiceClientFactory serviceClientFactory,
            IOptions<NuGetInsightsWorkerSettings> options)
        {
            _serviceClientFactory = serviceClientFactory;
            _options = options;

            _workInitializationState = ContainerInitializationState.Queue(serviceClientFactory, GetQueueName(QueueType.Work, poison: false));
            _workPoisonInitializationState = ContainerInitializationState.Queue(serviceClientFactory, GetQueueName(QueueType.Work, poison: true));
            _expandInitializationState = ContainerInitializationState.Queue(serviceClientFactory, GetQueueName(QueueType.Expand, poison: false));
            _expandPoisonInitializationState = ContainerInitializationState.Queue(serviceClientFactory, GetQueueName(QueueType.Expand, poison: true));
        }

        public async Task InitializeAsync()
        {
            await Task.WhenAll(
                _workInitializationState.InitializeAsync(),
                _workPoisonInitializationState.InitializeAsync(),
                _expandInitializationState.InitializeAsync(),
                _expandPoisonInitializationState.InitializeAsync());
        }

        public async Task<QueueClient> GetQueueAsync(QueueType type)
        {
            return (await _serviceClientFactory.GetQueueServiceClientAsync())
                .GetQueueClient(GetQueueName(type, poison: false));
        }

        public async Task<QueueClient> GetPoisonQueueAsync(QueueType type)
        {
            return (await _serviceClientFactory.GetQueueServiceClientAsync())
                .GetQueueClient(GetQueueName(type, poison: true));
        }

        private string GetQueueName(QueueType type, bool poison)
        {
            var queueName = type switch
            {
                QueueType.Work => _options.Value.WorkQueueName,
                QueueType.Expand => _options.Value.ExpandQueueName,
                _ => throw new NotImplementedException(),
            };

            if (poison)
            {
                return queueName + "-poison";
            }
            else
            {
                return queueName;
            }
        }
    }
}

using System;
using System.Linq;
using System.Threading.Tasks;
using Azure.Storage.Queues;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Worker
{
    public class WorkerQueueFactory : IWorkerQueueFactory
    {
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;

        public WorkerQueueFactory(
            ServiceClientFactory serviceClientFactory,
            IOptions<ExplorePackagesWorkerSettings> options)
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

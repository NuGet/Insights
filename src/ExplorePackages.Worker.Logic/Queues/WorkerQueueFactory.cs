using System.Threading.Tasks;
using Azure.Storage.Queues;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Worker
{
    public class WorkerQueueFactory : IWorkerQueueFactory
    {
        private readonly NewServiceClientFactory _serviceClientFactory;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;

        public WorkerQueueFactory(
            NewServiceClientFactory serviceClientFactory,
            IOptions<ExplorePackagesWorkerSettings> options)
        {
            _serviceClientFactory = serviceClientFactory;
            _options = options;
        }

        public async Task InitializeAsync()
        {
            await (await GetQueueAsync()).CreateIfNotExistsAsync(retry: true);
            await (await GetPoisonQueueAsync()).CreateIfNotExistsAsync(retry: true);
        }

        public async Task<QueueClient> GetQueueAsync()
        {
            return (await _serviceClientFactory.GetQueueServiceClientAsync())
                .GetQueueClient(_options.Value.WorkerQueueName);
        }

        public async Task<QueueClient> GetPoisonQueueAsync()
        {
            return (await _serviceClientFactory.GetQueueServiceClientAsync())
                .GetQueueClient(_options.Value.WorkerQueueName + "-poison");
        }
    }
}

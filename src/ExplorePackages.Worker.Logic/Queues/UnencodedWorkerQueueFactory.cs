using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage.Queue;

namespace Knapcode.ExplorePackages.Worker
{
    public class UnencodedWorkerQueueFactory : IWorkerQueueFactory
    {
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;

        public UnencodedWorkerQueueFactory(
            ServiceClientFactory serviceClientFactory,
            IOptions<ExplorePackagesWorkerSettings> options)
        {
            _serviceClientFactory = serviceClientFactory;
            _options = options;
        }

        public async Task InitializeAsync()
        {
            await GetQueue().CreateIfNotExistsAsync(retry: true);
            await GetPoisonQueue().CreateIfNotExistsAsync(retry: true);
        }

        public CloudQueue GetQueue()
        {
            return GetQueue(_options.Value.WorkerQueueName);
        }

        public CloudQueue GetPoisonQueue()
        {
            return GetQueue(_options.Value.WorkerQueueName + "-poison");
        }

        private CloudQueue GetQueue(string queueName)
        {
            var queue = _serviceClientFactory
                .GetStorageAccount()
                .CreateCloudQueueClient()
                .GetQueueReference(queueName);
            queue.EncodeMessage = false;
            return queue;
        }
    }
}

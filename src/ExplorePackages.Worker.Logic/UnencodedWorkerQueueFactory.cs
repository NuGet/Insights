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
        }

        public CloudQueue GetQueue()
        {
            var queue = _serviceClientFactory
                .GetStorageAccount()
                .CreateCloudQueueClient()
                .GetQueueReference(_options.Value.WorkerQueueName);
            queue.EncodeMessage = false;
            return queue;
        }
    }
}

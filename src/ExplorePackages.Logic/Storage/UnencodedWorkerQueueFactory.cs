using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage.Queue;

namespace Knapcode.ExplorePackages.Logic
{
    public class UnencodedWorkerQueueFactory : IWorkerQueueFactory
    {
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IOptionsSnapshot<ExplorePackagesSettings> _options;

        public UnencodedWorkerQueueFactory(
            ServiceClientFactory serviceClientFactory,
            IOptionsSnapshot<ExplorePackagesSettings> options)
        {
            _serviceClientFactory = serviceClientFactory;
            _options = options;
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

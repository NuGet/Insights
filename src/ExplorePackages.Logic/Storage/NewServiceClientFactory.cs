using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Azure.Storage.Queues;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages
{
    public class NewServiceClientFactory
    {
        private readonly IOptions<ExplorePackagesSettings> _options;
        private readonly Lazy<Task<QueueServiceClient>> _lazyQueueServiceClient;
        private readonly ConcurrentDictionary<string, Lazy<Task<QueueClient>>> _queueClients = new ConcurrentDictionary<string, Lazy<Task<QueueClient>>>();

        public NewServiceClientFactory(IOptions<ExplorePackagesSettings> options)
        {
            _options = options;
            _lazyQueueServiceClient = new Lazy<Task<QueueServiceClient>>(
                () => Task.FromResult(new QueueServiceClient(options.Value.StorageConnectionString)));
        }

        public async Task<QueueServiceClient> GetQueueServiceClientAsync()
        {
            return await _lazyQueueServiceClient.Value;
        }

        public async Task<QueueClient> GetQueueClientAsync(string name)
        {
            return await _queueClients.GetOrAdd(name, _ => new Lazy<Task<QueueClient>>(async () =>
            {
                var serviceClient = await _lazyQueueServiceClient.Value;
                return serviceClient.GetQueueClient(name);
            })).Value;
        }
    }
}

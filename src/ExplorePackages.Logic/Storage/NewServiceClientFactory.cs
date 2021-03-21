using System;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages
{
    public class NewServiceClientFactory
    {
        private readonly IOptions<ExplorePackagesSettings> _options;
        private readonly Lazy<Task<QueueServiceClient>> _lazyQueueServiceClient;
        private readonly Lazy<Task<BlobServiceClient>> _lazyBlobServiceClient;

        public NewServiceClientFactory(IOptions<ExplorePackagesSettings> options)
        {
            _options = options;
            _lazyQueueServiceClient = new Lazy<Task<QueueServiceClient>>(
                () => Task.FromResult(new QueueServiceClient(options.Value.StorageConnectionString)));
            _lazyBlobServiceClient = new Lazy<Task<BlobServiceClient>>(
                () => Task.FromResult(new BlobServiceClient(options.Value.StorageConnectionString)));
        }

        public async Task<QueueServiceClient> GetQueueServiceClientAsync()
        {
            return await _lazyQueueServiceClient.Value;
        }

        public async Task<BlobServiceClient> GetBlobServiceClientAsync()
        {
            return await _lazyBlobServiceClient.Value;
        }
    }
}

using System;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages
{
    public class ServiceClientFactory
    {
        private readonly IOptions<ExplorePackagesSettings> _options;
        private readonly Lazy<Task<QueueServiceClient>> _lazyQueueServiceClient;
        private readonly Lazy<Task<BlobServiceClient>> _lazyBlobServiceClient;
        private readonly Lazy<Task<TableServiceClient>> _lazyTableServiceClient;

        public ServiceClientFactory(IOptions<ExplorePackagesSettings> options)
        {
            _options = options;
            _lazyQueueServiceClient = new Lazy<Task<QueueServiceClient>>(
                () => Task.FromResult(new QueueServiceClient(options.Value.StorageConnectionString)));
            _lazyBlobServiceClient = new Lazy<Task<BlobServiceClient>>(
                () => Task.FromResult(new BlobServiceClient(options.Value.StorageConnectionString)));
            _lazyTableServiceClient = new Lazy<Task<TableServiceClient>>(
                () => Task.FromResult(new TableServiceClient(options.Value.StorageConnectionString)));
        }

        public async Task<QueueServiceClient> GetQueueServiceClientAsync()
        {
            return await _lazyQueueServiceClient.Value;
        }

        public async Task<BlobServiceClient> GetBlobServiceClientAsync()
        {
            return await _lazyBlobServiceClient.Value;
        }

        public async Task<TableServiceClient> GetTableServiceClientAsync()
        {
            return await _lazyTableServiceClient.Value;
        }
    }
}

using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages
{
    public class ServiceClientFactory
    {
        private readonly Lazy<string> _lazyConnectionString;
        private readonly Lazy<Task<BlobServiceClient>> _lazyBlobServiceClient;
        private readonly Lazy<Task<QueueServiceClient>> _lazyQueueServiceClient;
        private readonly Lazy<Task<TableServiceClient>> _lazyTableServiceClient;
        private readonly IOptions<ExplorePackagesSettings> _options;
        private readonly ILogger<ServiceClientFactory> _logger;

        public ServiceClientFactory(IOptions<ExplorePackagesSettings> options, ILogger<ServiceClientFactory> logger)
        {
            _options = options;
            _logger = logger;
            _lazyConnectionString = new Lazy<string>(() => GetStorageConnectionString());
            _lazyBlobServiceClient = new Lazy<Task<BlobServiceClient>>(() => Task.FromResult(GetBlobServiceClient()));
            _lazyQueueServiceClient = new Lazy<Task<QueueServiceClient>>(() => Task.FromResult(GetQueueServiceClient()));
            _lazyTableServiceClient = new Lazy<Task<TableServiceClient>>(() => Task.FromResult(GetTableServiceClient()));
        }

        private BlobServiceClient GetBlobServiceClient()
        {
            return new BlobServiceClient(_lazyConnectionString.Value);
        }

        private QueueServiceClient GetQueueServiceClient()
        {
            return new QueueServiceClient(_lazyConnectionString.Value);
        }

        private TableServiceClient GetTableServiceClient()
        {
            var settings = _options.Value;
            if (settings.StorageAccountName != null && settings.StorageSharedAccessSignature != null)
            {
                // Workaround for https://github.com/Azure/azure-sdk-for-net/issues/20082
                var endpoint = $"https://{settings.StorageAccountName}.table.core.windows.net/";
                _logger.LogInformation("Using table endpoint '{TableEndpoint}' and a SAS token.", endpoint);
                return new TableServiceClient(new Uri(endpoint), new AzureSasCredential(settings.StorageSharedAccessSignature));
            }

            return new TableServiceClient(_lazyConnectionString.Value);
        }

        public string GetStorageConnectionString()
        {
            return GetStorageConnectionString(_options.Value, _logger);
        }

        private static string GetStorageConnectionString(ExplorePackagesSettings settings, ILogger logger)
        {
            string connectionString;
            if (settings.StorageAccountName != null && settings.StorageSharedAccessSignature != null)
            {
                var parsedSas = QueryHelpers.ParseQuery(settings.StorageSharedAccessSignature);
                var expiry = parsedSas["se"].Single();
                var parsedExpiry = DateTimeOffset.Parse(expiry);
                var untilExpiry = parsedExpiry - DateTimeOffset.UtcNow;

                logger.LogInformation(
                    "Using storage account '{StorageAccountName}' and a SAS token expiring at {Expiry}, which is in {RemainingHours:F2} hours.",
                    settings.StorageAccountName,
                    expiry,
                    untilExpiry.TotalHours);

                connectionString = $"AccountName={settings.StorageAccountName};SharedAccessSignature={settings.StorageSharedAccessSignature}";
            }
            else
            {
                logger.LogInformation("Using the configured storage connection string.");
                connectionString = settings.StorageConnectionString;
            }

            return connectionString;
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

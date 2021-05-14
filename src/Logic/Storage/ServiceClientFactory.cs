using System;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Azure.Storage.Sas;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages
{
    public class ServiceClientFactory
    {
        private static readonly TimeSpan DefaultRefreshPeriod = TimeSpan.FromHours(1);

        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1);
        private ServiceClients _serviceClients;

        private readonly IOptions<ExplorePackagesSettings> _options;
        private readonly ILogger<ServiceClientFactory> _logger;

        public ServiceClientFactory(
            AzureLoggingStartup loggingStartup, // Injected so that logging starts
            IOptions<ExplorePackagesSettings> options,
            ILogger<ServiceClientFactory> logger)
        {
            _options = options;
            _logger = logger;
        }

        public async Task<SecretClient> GetKeyVaultSecretClientAsync()
        {
            return (await GetCachedServiceClientsAsync()).KeyVaultSecretClient;
        }

        public async Task<QueueServiceClient> GetQueueServiceClientAsync()
        {
            return (await GetCachedServiceClientsAsync()).QueueServiceClient;
        }

        public async Task<BlobServiceClient> GetBlobServiceClientAsync()
        {
            return (await GetCachedServiceClientsAsync()).BlobServiceClient;
        }

        public async Task<TableServiceClient> GetTableServiceClientAsync()
        {
            return (await GetCachedServiceClientsAsync()).TableServiceClient;
        }

        public async Task<string> GetBlobReadStorageSharedAccessSignatureAsync()
        {
            var clients = await GetCachedServiceClientsAsync();
            if (clients.BlobServiceClient.CanGenerateAccountSasUri)
            {
                var uri = clients.BlobServiceClient.GenerateAccountSasUri(
                    permissions: AccountSasPermissions.Read,
                    expiresOn: DateTimeOffset.UtcNow.AddDays(7),
                    resourceTypes: AccountSasResourceTypes.Object);
                return uri.Query;
            }
            else if (clients.BlobReadStorageSharedAccessSignature != null)
            {
                return clients.BlobReadStorageSharedAccessSignature;
            }

            throw new NotSupportedException("Generating blob read SAS tokens is not supported with the current configuration.");
        }

        public string GetStorageConnectionStringSync()
        {
            var clients = GetCachedServiceClientsSync();
            return clients.StorageConnectionString;
        }

        private async Task<ServiceClients> GetCachedServiceClientsAsync(CancellationToken token = default)
        {
            if (TryGetServiceClients(out var serviceClients))
            {
                return serviceClients;
            }

            await _lock.WaitAsync(token);
            try
            {
                if (TryGetServiceClients(out serviceClients))
                {
                    return serviceClients;
                }

                _serviceClients = await GetServiceClientsAsync(token);
                return _serviceClients;
            }
            finally
            {
                _lock.Release();
            }
        }

        private ServiceClients GetCachedServiceClientsSync()
        {
            if (TryGetServiceClients(out var serviceClients))
            {
                return serviceClients;
            }

            _lock.Wait();
            try
            {
                if (TryGetServiceClients(out serviceClients))
                {
                    return serviceClients;
                }

                _serviceClients = GetServiceClientsSync();
                return _serviceClients;
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task<ServiceClients> GetServiceClientsAsync(CancellationToken token)
        {
            var created = DateTimeOffset.UtcNow;
            var secretClient = GetSecretClient();
            string appSasFromKeyVault = null;
            string blobReadSasFromKeyVault = null;
            if (secretClient != null)
            {
                KeyVaultSecret appSas = await secretClient.GetSecretAsync(
                    _options.Value.StorageSharedAccessSignatureSecretName,
                    cancellationToken: token);
                appSasFromKeyVault = appSas.Value;

                KeyVaultSecret blobReadSas = await secretClient.GetSecretAsync(
                    _options.Value.StorageBlobReadSharedAccessSignatureSecretName,
                    cancellationToken: token);
                blobReadSasFromKeyVault = blobReadSas.Value;
            }

            return GetServiceClients(created, secretClient, appSasFromKeyVault, blobReadSasFromKeyVault);
        }

        private ServiceClients GetServiceClientsSync()
        {
            var created = DateTimeOffset.UtcNow;
            var secretClient = GetSecretClient();
            string appSasFromKeyVault = null;
            string blobReadSasFromKeyVault = null;
            if (secretClient != null)
            {
                KeyVaultSecret appSas = secretClient.GetSecret(_options.Value.StorageSharedAccessSignatureSecretName);
                appSasFromKeyVault = appSas.Value;

                KeyVaultSecret blobReadSas = secretClient.GetSecret(_options.Value.StorageBlobReadSharedAccessSignatureSecretName);
                blobReadSasFromKeyVault = blobReadSas.Value;
            }

            return GetServiceClients(created, secretClient, appSasFromKeyVault, blobReadSasFromKeyVault);
        }

        private bool TryGetServiceClients(out ServiceClients serviceClients)
        {
            serviceClients = _serviceClients;
            var untilRefresh = GetTimeUntilRefresh(serviceClients);
            if (untilRefresh <= TimeSpan.Zero)
            {
                return false;
            }

            return true;
        }

        private TimeSpan GetTimeUntilRefresh(ServiceClients serviceClients)
        {
            if (serviceClients == null)
            {
                // The service clients have never been initialized (successfully).
                return TimeSpan.Zero;
            }

            TimeSpan untilRefresh;
            if (serviceClients.AppStorageSharedAccessSignature != null)
            {
                // Refresh at half of the SAS duration.
                var originalDuration = serviceClients.StorageSharedAccessSignatureExpiry.Value - serviceClients.Created;
                var halfUntilExpiry = serviceClients.Created + (originalDuration / 2);
                untilRefresh = halfUntilExpiry - DateTimeOffset.UtcNow;
            }
            else
            {
                // Otherwise, refresh at a default rate.
                var defaultRefresh = serviceClients.Created + DefaultRefreshPeriod;
                untilRefresh = defaultRefresh - DateTimeOffset.UtcNow;
            }

            return untilRefresh > TimeSpan.Zero ? untilRefresh : TimeSpan.Zero;
        }

        private SecretClient GetSecretClient()
        {
            if (_options.Value.StorageAccountName != null
                && _options.Value.KeyVaultName != null
                && _options.Value.StorageSharedAccessSignatureSecretName != null)
            {
                var vaultUri = new Uri($"https://{_options.Value.KeyVaultName}.vault.azure.net/");
                var secretClient = new SecretClient(vaultUri, new DefaultAzureCredential());
                return secretClient;
            }

            return null;
        }

        private ServiceClients GetServiceClients(DateTimeOffset created, SecretClient secretClient, string appSasFromKeyVault, string blobReadSasFromKeyVault)
        {
            string appSas;
            DateTimeOffset? sasExpiry;
            string storageConnectionString;
            if (_options.Value.StorageAccountName != null)
            {
                string source;
                if (appSasFromKeyVault != null)
                {
                    appSas = appSasFromKeyVault;
                    source = "Key Vault";
                }
                else
                {
                    appSas = _options.Value.StorageSharedAccessSignature;
                    source = "config";
                }

                if (appSas == null)
                {
                    throw new InvalidOperationException($"No storage SAS token could be found.");
                }

                sasExpiry = StorageUtility.GetSasExpiry(appSas);
                var untilExpiry = sasExpiry.Value - DateTimeOffset.UtcNow;

                _logger.LogInformation(
                    "Using storage account '{StorageAccountName}' and a SAS token from {Source} expiring at {Expiry:O}, which is in {RemainingHours:F2} hours.",
                    _options.Value.StorageAccountName,
                    source,
                    sasExpiry,
                    untilExpiry.TotalHours);

                storageConnectionString = $"AccountName={_options.Value.StorageAccountName};SharedAccessSignature={appSas}";
            }
            else
            {
                _logger.LogInformation("Using the configured storage connection string.");

                appSas = null;
                sasExpiry = null;
                storageConnectionString = _options.Value.StorageConnectionString;
            }

            var blob = new BlobServiceClient(storageConnectionString, new BlobClientOptions
            {
                Diagnostics = { IsLoggingEnabled = _options.Value.EnableAzureLogging }
            });
            var queue = new QueueServiceClient(storageConnectionString, new QueueClientOptions
            {
                Diagnostics = { IsLoggingEnabled = _options.Value.EnableAzureLogging }
            });
            var table = new TableServiceClient(storageConnectionString, new TablesClientOptions
            {
                Diagnostics = { IsLoggingEnabled = _options.Value.EnableAzureLogging }
            });
            
            _logger.LogInformation("Blob endpoint: {BlobEndpoint}", blob.Uri);
            _logger.LogInformation("Queue endpoint: {QueueEndpoint}", queue.Uri);
            // No Uri property for TableServiceClient, see https://github.com/Azure/azure-sdk-for-net/issues/19881

            return new ServiceClients(
                created,
                appSas,
                blobReadSasFromKeyVault ?? _options.Value.StorageBlobReadSharedAccessSignature,
                sasExpiry,
                storageConnectionString,
                secretClient,
                blob,
                queue,
                table);
        }

        private record ServiceClients(
            DateTimeOffset Created,
            string AppStorageSharedAccessSignature,
            string BlobReadStorageSharedAccessSignature,
            DateTimeOffset? StorageSharedAccessSignatureExpiry,
            string StorageConnectionString,
            SecretClient KeyVaultSecretClient,
            BlobServiceClient BlobServiceClient,
            QueueServiceClient QueueServiceClient,
            TableServiceClient TableServiceClient);
    }
}

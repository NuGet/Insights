using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.AspNetCore.WebUtilities;
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

        public ServiceClientFactory(IOptions<ExplorePackagesSettings> options, ILogger<ServiceClientFactory> logger)
        {
            _options = options;
            _logger = logger;
        }

        public TimeSpan UntilRefresh => GetTimeUntilRefresh(_serviceClients);

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

        public (string ConnectionString, TimeSpan UntilRefresh) GetStorageConnectionSync()
        {
            var clients = GetCachedServiceClientsSync();
            return (clients.StorageConnectionString, GetTimeUntilRefresh(clients));
        }

        public async Task<(string ConnectionString, TimeSpan UntilRefresh)> GetStorageConnectionAsync(CancellationToken token)
        {
            var clients = await GetCachedServiceClientsAsync(token);
            return (clients.StorageConnectionString, GetTimeUntilRefresh(clients));
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
            string sasFromKeyVault = null;
            if (secretClient != null)
            {
                KeyVaultSecret secret = await secretClient.GetSecretAsync(
                    _options.Value.StorageSharedAccessSignatureSecretName,
                    cancellationToken: token);
                sasFromKeyVault = secret.Value;
            }

            return GetServiceClients(created, secretClient, sasFromKeyVault);
        }

        private ServiceClients GetServiceClientsSync()
        {
            var created = DateTimeOffset.UtcNow;
            var secretClient = GetSecretClient();
            string sasFromKeyVault = null;
            if (secretClient != null)
            {
                KeyVaultSecret secret = secretClient.GetSecret(_options.Value.StorageSharedAccessSignatureSecretName);
                sasFromKeyVault = secret.Value;
            }

            return GetServiceClients(created, secretClient, sasFromKeyVault);
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
            if (serviceClients.StorageSharedAccessSignature != null)
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

        private ServiceClients GetServiceClients(DateTimeOffset created, SecretClient secretClient, string sasFromKeyVault)
        {
            string sas;
            DateTimeOffset? sasExpiry;
            string storageConnectionString;
            TableServiceClient tableServiceClient;
            if (_options.Value.StorageAccountName != null)
            {
                string source;
                if (sasFromKeyVault != null)
                {
                    sas = sasFromKeyVault;
                    source = "Key Vault";
                }
                else
                {
                    sas = _options.Value.StorageSharedAccessSignature;
                    source = "config";
                }

                if (sas == null)
                {
                    throw new InvalidOperationException($"No storage SAS token could be found.");
                }

                sasExpiry = StorageUtility.GetSasExpiry(sas);
                var untilExpiry = sasExpiry.Value - DateTimeOffset.UtcNow;

                _logger.LogInformation(
                    "Using storage account '{StorageAccountName}' and a SAS token from {Source} expiring at {Expiry:O}, which is in {RemainingMinutes:F2} minutes.",
                    _options.Value.StorageAccountName,
                    source,
                    sasExpiry,
                    untilExpiry.TotalMinutes);

                storageConnectionString = $"AccountName={_options.Value.StorageAccountName};SharedAccessSignature={sas}";
                var endpoint = $"https://{_options.Value.StorageAccountName}.table.core.windows.net/";
                tableServiceClient = new TableServiceClient(new Uri(endpoint), new AzureSasCredential(sas));
            }
            else
            {
                _logger.LogInformation("Using the configured storage connection string.");

                sas = null;
                sasExpiry = null;
                storageConnectionString = _options.Value.StorageConnectionString;
                tableServiceClient = new TableServiceClient(storageConnectionString);
            }

            return new ServiceClients(
                created,
                sas,
                sasExpiry,
                storageConnectionString,
                secretClient,
                new BlobServiceClient(storageConnectionString),
                new QueueServiceClient(storageConnectionString),
                tableServiceClient);
        }

        private record ServiceClients(
            DateTimeOffset Created,
            string StorageSharedAccessSignature,
            DateTimeOffset? StorageSharedAccessSignatureExpiry,
            string StorageConnectionString,
            SecretClient KeyVaultSecretClient,
            BlobServiceClient BlobServiceClient,
            QueueServiceClient QueueServiceClient,
            TableServiceClient TableServiceClient);
    }
}

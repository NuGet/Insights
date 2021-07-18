// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using Azure.Storage.Sas;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NuGet.Insights
{
    public class ServiceClientFactory
    {
        private static readonly TimeSpan DefaultRefreshPeriod = TimeSpan.FromHours(1);

        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1);
        private ServiceClients _serviceClients;

        private readonly IOptions<NuGetInsightsSettings> _options;
        private readonly ILogger<ServiceClientFactory> _logger;

        public ServiceClientFactory(
            IOptions<NuGetInsightsSettings> options,
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

        public async Task<Uri> GetBlobReadUrlAsync(string containerName, string blobName)
        {
            var serviceClients = await GetCachedServiceClientsAsync();
            var blobClient = serviceClients
                .BlobServiceClient
                .GetBlobContainerClient(containerName)
                .GetBlobClient(blobName);

            if (_options.Value.StorageBlobReadSharedAccessSignature != null)
            {
                return new UriBuilder(blobClient.Uri) { Query = _options.Value.StorageBlobReadSharedAccessSignature }.Uri;
            }

            if (serviceClients.UserDelegationKey != null)
            {
                var blobReadSasBuilder = new BlobSasBuilder(BlobContainerSasPermissions.Read, serviceClients.SharedAccessSignatureExpiry)
                {
                    BlobContainerName = containerName,
                    BlobName = blobName,
                };
                var sas = blobReadSasBuilder
                    .ToSasQueryParameters(serviceClients.UserDelegationKey, serviceClients.BlobServiceClient.AccountName)
                    .ToString();

                return new UriBuilder(blobClient.Uri) { Query = sas }.Uri;
            }

            return serviceClients
                .BlobServiceClient
                .GetBlobContainerClient(containerName)
                .GetBlobClient(blobName)
                .GenerateSasUri(BlobSasPermissions.Read, serviceClients.SharedAccessSignatureExpiry);
        }

        public async Task<TableServiceClient> GetTableServiceClientAsync()
        {
            return (await GetCachedServiceClientsAsync()).TableServiceClient;
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

        private async Task<ServiceClients> GetServiceClientsAsync(CancellationToken token)
        {
            var created = DateTimeOffset.UtcNow;
            var secretClient = GetSecretClient();
            string appSasFromKeyVault = null;
            if (secretClient != null)
            {
                KeyVaultSecret appSas = await secretClient.GetSecretAsync(
                    _options.Value.TableSharedAccessSignatureSecretName,
                    cancellationToken: token);
                appSasFromKeyVault = appSas.Value;
            }

            return await GetServiceClientsAsync(created, secretClient, appSasFromKeyVault);
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

            // Refresh at half of the SAS duration.
            var originalDuration = serviceClients.SharedAccessSignatureExpiry - serviceClients.Created;
            var halfUntilExpiry = serviceClients.Created + (originalDuration / 2);
            var untilRefresh = halfUntilExpiry - DateTimeOffset.UtcNow;

            return untilRefresh > TimeSpan.Zero ? untilRefresh : TimeSpan.Zero;
        }

        private SecretClient GetSecretClient()
        {
            if (_options.Value.StorageAccountName != null
                && _options.Value.KeyVaultName != null
                && _options.Value.TableSharedAccessSignatureSecretName != null)
            {
                var vaultUri = new Uri($"https://{_options.Value.KeyVaultName}.vault.azure.net/");
                var secretClient = new SecretClient(vaultUri, new DefaultAzureCredential());
                return secretClient;
            }

            return null;
        }

        private async Task<ServiceClients> GetServiceClientsAsync(DateTimeOffset created, SecretClient secretClient, string tableSasFromKeyVault)
        {
            BlobServiceClient blob;
            QueueServiceClient queue;
            TableServiceClient table;
            UserDelegationKey userDelegationKey;
            DateTimeOffset sasExpiry;
            if (_options.Value.StorageAccountName != null)
            {
                blob = new BlobServiceClient(new Uri($"https://{_options.Value.StorageAccountName}.blob.core.windows.net"), new DefaultAzureCredential());
                queue = new QueueServiceClient(new Uri($"https://{_options.Value.StorageAccountName}.queue.core.windows.net"), new DefaultAzureCredential());
                table = new TableServiceClient($"AccountName={_options.Value.StorageAccountName};SharedAccessSignature={tableSasFromKeyVault}");

                sasExpiry = StorageUtility.GetSasExpiry(tableSasFromKeyVault);
                userDelegationKey = await blob.GetUserDelegationKeyAsync(startsOn: null, expiresOn: sasExpiry);

                _logger.LogInformation(
                    "Using storage account '{StorageAccountName}' and a SAS token from Key Vault expiring at {Expiry:O}, which is in {RemainingHours:F2} hours.",
                    _options.Value.StorageAccountName,
                    sasExpiry,
                    (sasExpiry - DateTimeOffset.UtcNow).TotalHours);
            }
            else
            {
                _logger.LogInformation("Using the configured storage connection string.");

                blob = new BlobServiceClient(_options.Value.StorageConnectionString);
                queue = new QueueServiceClient(_options.Value.StorageConnectionString);
                table = new TableServiceClient(_options.Value.StorageConnectionString);

                userDelegationKey = null;
                sasExpiry = DateTimeOffset.UtcNow.Add(2 * DefaultRefreshPeriod);
            }

            _logger.LogInformation("Blob endpoint: {BlobEndpoint}", blob.Uri);
            _logger.LogInformation("Queue endpoint: {QueueEndpoint}", queue.Uri);
            // No Uri property for TableServiceClient, see https://github.com/Azure/azure-sdk-for-net/issues/19881

            return new ServiceClients(
                created,
                userDelegationKey,
                sasExpiry,
                secretClient,
                blob,
                queue,
                table);
        }

        private record ServiceClients(
            DateTimeOffset Created,
            UserDelegationKey UserDelegationKey,
            DateTimeOffset SharedAccessSignatureExpiry,
            SecretClient KeyVaultSecretClient,
            BlobServiceClient BlobServiceClient,
            QueueServiceClient QueueServiceClient,
            TableServiceClient TableServiceClient);
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography.X509Certificates;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Data.Tables;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using Azure.Storage.Sas;
using NuGet.Insights.StorageNoOpRetry;

namespace NuGet.Insights
{
    public class ServiceClientFactory
    {
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1);
        private ServiceClients _serviceClients;
        private readonly Func<HttpClient> _httpClientFactory;
        private readonly IOptions<NuGetInsightsSettings> _options;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<ServiceClientFactory> _logger;

        public ServiceClientFactory(
            IOptions<NuGetInsightsSettings> options,
            ILoggerFactory loggerFactory) : this(httpClientFactory: null, options, loggerFactory)
        {
        }

        public ServiceClientFactory(
            Func<HttpClient> httpClientFactory,
            IOptions<NuGetInsightsSettings> options,
            ILoggerFactory loggerFactory)
        {
            _httpClientFactory = httpClientFactory;
            _options = options;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<ServiceClientFactory>();
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

        public async Task<TableServiceClientWithRetryContext> GetTableServiceClientAsync()
        {
            return (await GetCachedServiceClientsAsync()).TableServiceClientWithRetryContext;
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

                _serviceClients = await GetServiceClientsAsync(created: DateTimeOffset.UtcNow);
                return _serviceClients;
            }
            finally
            {
                _lock.Release();
            }
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

            // Refresh at half of the SAS duration or the default refresh period, whichever is lesser.
            var sasDuration = serviceClients.SharedAccessSignatureExpiry - serviceClients.Created;
            var refreshPeriod = TimeSpan.FromTicks(Math.Min(sasDuration.Ticks / 2, _options.Value.ServiceClientRefreshPeriod.Ticks));
            var sinceCreated = DateTimeOffset.UtcNow - serviceClients.Created;
            var untilRefresh = refreshPeriod - sinceCreated;

            return untilRefresh > TimeSpan.Zero ? untilRefresh : TimeSpan.Zero;
        }

        private T GetOptions<T>(T options, HttpPipelineTransport transport) where T : ClientOptions
        {
            if (transport is not null)
            {
                options.Transport = transport;
            };

            const int maxRetries = 2;
            options.Retry.MaxRetries = maxRetries;
            options.Retry.NetworkTimeout = ServiceCollectionExtensions.HttpClientTimeout;
            options.RetryPolicy = new StorageNoOpRetryPolicy(
                _loggerFactory.CreateLogger<StorageNoOpRetryPolicy>(),
                maxRetries);

            return options;
        }

        private async Task<ServiceClients> GetServiceClientsAsync(DateTimeOffset created)
        {
            var httpPipelineTransport = GetHttpPipelineTransport();

            BlobServiceClient blob;
            QueueServiceClient queue;
            TableServiceClient table;
            UserDelegationKey userDelegationKey;
            DateTimeOffset sasExpiry = DateTimeOffset.UtcNow.Add(_options.Value.ServiceClientSasDuration);

            if (_options.Value.StorageAccountName is not null)
            {
                TokenCredential credential;
                string credentialType;
                if (_options.Value.StorageClientCertificatePath is not null)
                {
                    credential = await CredentialCache.GetLazyClientCertificateCredentialTask(
                        _options.Value.StorageClientTenantId,
                        _options.Value.StorageClientApplicationId,
                        () =>
                        {
                            var certificate = new X509Certificate2(_options.Value.StorageClientCertificatePath);
                            return Task.FromResult(certificate);
                        }).Value;
                    credentialType = "client certificate credential from disk";
                }
                else if (_options.Value.StorageClientCertificateKeyVault is not null)
                {
                    credential = await CredentialCache.GetLazyClientCertificateCredentialTask(
                        _options.Value.StorageClientTenantId,
                        _options.Value.StorageClientApplicationId,
                        () => CredentialCache.GetLazyCertificateTask(
                            _options.Value.StorageClientCertificateKeyVault,
                            _options.Value.StorageClientCertificateKeyVaultCertificateName).Value).Value;
                    credentialType = "client certificate credential from KeyVault";
                }
                else if (_options.Value.UserManagedIdentityClientId is not null)
                {
                    credential = new ManagedIdentityCredential(
                        _options.Value.UserManagedIdentityClientId);
                    credentialType = "user managed identity credential";
                }
                else
                {
                    credential = CredentialCache.DefaultAzureCredential;
                    credentialType = "default Azure credential";
                }

                blob = new BlobServiceClient(
                    new Uri($"https://{_options.Value.StorageAccountName}.blob.core.windows.net"),
                    credential,
                    GetOptions(new BlobClientOptions(), httpPipelineTransport));

                queue = new QueueServiceClient(
                    new Uri($"https://{_options.Value.StorageAccountName}.queue.core.windows.net"),
                    credential,
                    GetOptions(new QueueClientOptions(), httpPipelineTransport));

                table = new TableServiceClient(
                    new Uri($"https://{_options.Value.StorageAccountName}.table.core.windows.net"),
                    credential,
                    GetOptions(new TableClientOptions(), httpPipelineTransport));

                _logger.LogInformation(
                    "Using storage account '{StorageAccountName}' with a {CredentialType} and a user delegation key expiring at {Expiry:O}, which is in {RemainingHours:F2} hours.",
                    _options.Value.StorageAccountName,
                    credentialType,
                    sasExpiry,
                    (sasExpiry - DateTimeOffset.UtcNow).TotalHours);

                userDelegationKey = await blob.GetUserDelegationKeyAsync(startsOn: null, expiresOn: sasExpiry);
            }
            else
            {
                _logger.LogInformation("Using the configured storage connection string.");

                blob = new BlobServiceClient(
                    _options.Value.StorageConnectionString,
                    GetOptions(new BlobClientOptions(), httpPipelineTransport));

                queue = new QueueServiceClient(
                    _options.Value.StorageConnectionString,
                    GetOptions(new QueueClientOptions(), httpPipelineTransport));

                table = new TableServiceClient(
                    _options.Value.StorageConnectionString,
                    GetOptions(new TableClientOptions(), httpPipelineTransport));

                userDelegationKey = null;
            }

            _logger.LogInformation("Blob endpoint: {BlobEndpoint}", blob.Uri.Obfuscate());
            _logger.LogInformation("Queue endpoint: {QueueEndpoint}", queue.Uri.Obfuscate());
            _logger.LogInformation("Table endpoint: {TableEndpoint}", table.Uri.Obfuscate());

            return new ServiceClients(
                created,
                userDelegationKey,
                sasExpiry,
                blob,
                queue,
                table,
                new TableServiceClientWithRetryContext(table));
        }

        protected virtual HttpPipelineTransport GetHttpPipelineTransport()
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                var httpClient = _httpClientFactory?.Invoke();
                return httpClient != null ? new HttpClientTransport(httpClient) : null;
            }

            return null;
        }

        private record ServiceClients(
            DateTimeOffset Created,
            UserDelegationKey UserDelegationKey,
            DateTimeOffset SharedAccessSignatureExpiry,
            BlobServiceClient BlobServiceClient,
            QueueServiceClient QueueServiceClient,
            TableServiceClient TableServiceClient,
            TableServiceClientWithRetryContext TableServiceClientWithRetryContext);
    }
}

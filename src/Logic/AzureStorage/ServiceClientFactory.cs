// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography.X509Certificates;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Data.Tables;
using Azure.Identity;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using Azure.Storage.Sas;
using NuGet.Insights.StorageNoOpRetry;

#nullable enable

namespace NuGet.Insights
{
    public class ServiceClientFactory
    {
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1);
        private ServiceClients? _serviceClients;

        private readonly Func<HttpClient>? _httpClientFactory;
        private readonly IOptions<NuGetInsightsSettings> _options;
        private readonly ITelemetryClient _telemetryClient;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<ServiceClientFactory> _logger;

        public ServiceClientFactory(
            IOptions<NuGetInsightsSettings> options,
            ITelemetryClient telemetryClient,
            ILoggerFactory loggerFactory) : this(httpClientFactory: null, options, telemetryClient, loggerFactory)
        {
        }

        public ServiceClientFactory(
            Func<HttpClient>? httpClientFactory,
            IOptions<NuGetInsightsSettings> options,
            ITelemetryClient telemetryClient,
            ILoggerFactory loggerFactory)
        {
            _httpClientFactory = httpClientFactory;
            _options = options;
            _telemetryClient = telemetryClient;
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

        public async Task<BlobClient?> TryGetBlobClientAsync(Uri blobUrl)
        {
            var serviceClients = await GetCachedServiceClientsAsync();

            if (!serviceClients.BlobClientFactory.TryGetBlobClient(serviceClients.BlobServiceClient, blobUrl, out var blobClient))
            {
                return null;
            }

            return blobClient;
        }

        public async Task<Uri> GetBlobReadUrlAsync(string containerName, string blobName)
        {
            var serviceClients = await GetCachedServiceClientsAsync();
            var blobClient = serviceClients
                .BlobServiceClient
                .GetBlobContainerClient(containerName)
                .GetBlobClient(blobName);

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

                _serviceClients = await GetServiceClientsAsync(
                    created: DateTimeOffset.UtcNow,
                    GetHttpPipelineTransport(),
                    _options.Value,
                    _telemetryClient,
                    _logger,
                    _loggerFactory);

                return _serviceClients;
            }
            finally
            {
                _lock.Release();
            }
        }

        private bool TryGetServiceClients([NotNullWhen(true)] out ServiceClients? serviceClients)
        {
            serviceClients = _serviceClients;
            if (serviceClients is null)
            {
                return false;

            }

            var untilRefresh = GetTimeUntilRefresh(serviceClients);
            if (untilRefresh <= TimeSpan.Zero)
            {
                serviceClients = null;
                return false;
            }

            return true;
        }

        private TimeSpan GetTimeUntilRefresh(ServiceClients serviceClients)
        {
            // Refresh at half of the SAS duration or the default refresh period, whichever is lesser.
            var sasDuration = serviceClients.SharedAccessSignatureExpiry - serviceClients.Created;
            var refreshPeriod = TimeSpan.FromTicks(Math.Min(sasDuration.Ticks / 2, _options.Value.ServiceClientRefreshPeriod.Ticks));
            var sinceCreated = DateTimeOffset.UtcNow - serviceClients.Created;
            var untilRefresh = refreshPeriod - sinceCreated;

            return untilRefresh > TimeSpan.Zero ? untilRefresh : TimeSpan.Zero;
        }

        private static T GetOptions<T>(
            StorageSettings settings,
            ILoggerFactory loggerFactory,
            T options,
            HttpPipelineTransport? transport) where T : ClientOptions
        {
            if (transport is not null)
            {
                options.Transport = transport;
            }

            var maxRetries = settings.AzureServiceClientMaxRetries;
            options.Retry.MaxRetries = maxRetries;
            options.Retry.NetworkTimeout = settings.AzureServiceClientNetworkTimeout;
            options.RetryPolicy = new StorageNoOpRetryPolicy(
                loggerFactory.CreateLogger<StorageNoOpRetryPolicy>(),
                maxRetries);

            return options;
        }

        private static async Task<ServiceClients> GetServiceClientsAsync(
            DateTimeOffset created,
            HttpPipelineTransport? httpPipelineTransport,
            StorageSettings settings,
            ITelemetryClient telemetryClient,
            ILogger logger,
            ILoggerFactory loggerFactory)
        {
            var storageCredentialType = settings.GetStorageCredentialType();

            // build the credentials
            TokenCredential? tokenCredential = null;
            StorageSharedKeyCredential? storageAccessKeyCredential = null;
            TableSharedKeyCredential? tableAccessKeyCredential = null;
            switch (storageCredentialType)
            {
                case StorageCredentialType.DefaultAzureCredential:
                    tokenCredential = CredentialCache.DefaultAzureCredential;
                    break;

                case StorageCredentialType.UserAssignedManagedIdentityCredential:
                    tokenCredential = new ManagedIdentityCredential(
                        settings.UserManagedIdentityClientId);
                    break;

                case StorageCredentialType.ClientCertificateCredentialFromKeyVault:
                    tokenCredential = await CredentialCache.GetLazyClientCertificateCredentialTask(
                        settings.StorageClientTenantId,
                        settings.StorageClientApplicationId,
                        () => CredentialCache.GetLazyCertificateTask(
                            settings.StorageClientCertificateKeyVault,
                            settings.StorageClientCertificateKeyVaultCertificateName).Value).Value;
                    break;

                case StorageCredentialType.ClientCertificateCredentialFromPath:
                    tokenCredential = await CredentialCache.GetLazyClientCertificateCredentialTask(
                        settings.StorageClientTenantId,
                        settings.StorageClientApplicationId,
                        () =>
                        {
                            var certificate = new X509Certificate2(settings.StorageClientCertificatePath);
                            return Task.FromResult(certificate);
                        }).Value;
                    break;

                case StorageCredentialType.StorageAccessKey:
                    storageAccessKeyCredential = new StorageSharedKeyCredential(
                        settings.StorageAccountName,
                        settings.StorageAccessKey);
                    tableAccessKeyCredential = new TableSharedKeyCredential(
                        settings.StorageAccountName,
                        settings.StorageAccessKey);
                    break;

                case StorageCredentialType.DevelopmentStorage:
                case StorageCredentialType.MemoryStorage:
                    break;

                default:
                    throw new NotImplementedException();
            }

            // build the client options
            var blobOptions = GetOptions(settings, loggerFactory, new BlobClientOptions(), httpPipelineTransport);
            var queueOptions = GetOptions(settings, loggerFactory, new QueueClientOptions(), httpPipelineTransport);
            var tableOptions = GetOptions(settings, loggerFactory, new TableClientOptions(), httpPipelineTransport);

            // build the client factories
            IBlobClientFactory blobClientFactory;
            IQueueClientFactory queueClientFactory;
            ITableClientFactory tableClientFactory;
            bool supportsUserDelegationKey;
            if (storageCredentialType == StorageCredentialType.MemoryStorage)
            {
                blobClientFactory = new MemoryBlobClientFactory();
                queueClientFactory = new MemoryQueueClientFactory();
                tableClientFactory = new MemoryTableClientFactory();
                supportsUserDelegationKey = true;
            }
            else if (storageCredentialType == StorageCredentialType.DevelopmentStorage)
            {
                blobClientFactory = new DevelopmentBlobClientFactory(blobOptions);
                queueClientFactory = new DevelopmentQueueClientFactory(queueOptions);
                tableClientFactory = new DevelopmentTableClientFactory(tableOptions);
                supportsUserDelegationKey = false;
            }
            else
            {
                Uri blobServiceUri = StorageUtility.GetBlobEndpoint(settings.StorageAccountName);
                Uri queueServiceUri = StorageUtility.GetQueueEndpoint(settings.StorageAccountName);
                Uri tableServiceUri = StorageUtility.GetTableEndpoint(settings.StorageAccountName);

                if (tokenCredential is not null)
                {
                    blobClientFactory = new TokenCredentialBlobClientFactory(blobServiceUri, tokenCredential, blobOptions);
                    queueClientFactory = new TokenCredentialQueueClientFactory(queueServiceUri, tokenCredential, queueOptions);
                    tableClientFactory = new TokenCredentialTableClientFactory(tableServiceUri, tokenCredential, tableOptions);
                    supportsUserDelegationKey = true;
                }
                else if (storageAccessKeyCredential is not null && tableAccessKeyCredential is not null)
                {
                    blobClientFactory = new SharedKeyCredentialBlobClientFactory(blobServiceUri, storageAccessKeyCredential, blobOptions);
                    queueClientFactory = new SharedKeyCredentialQueueClientFactory(queueServiceUri, storageAccessKeyCredential, queueOptions);
                    tableClientFactory = new SharedKeyCredentialTableClientFactory(tableServiceUri, tableAccessKeyCredential, tableOptions);
                    supportsUserDelegationKey = false;
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            // build the service clients
            BlobServiceClient blob = blobClientFactory.GetServiceClient();
            QueueServiceClient queue = queueClientFactory.GetServiceClient();
            TableServiceClient table = tableClientFactory.GetServiceClient();

            var sasExpiry = created.Add(settings.ServiceClientSasDuration);
            logger.LogInformation(
                "Using storage account '{StorageAccountName}' with a {CredentialType} credential type. The service clients will be cached for {Duration} hours.",
                blob.AccountName,
                storageCredentialType,
                (sasExpiry - created).TotalHours);

            logger.LogInformation("Blob endpoint: {BlobEndpoint}", blob.Uri.Obfuscate());
            logger.LogInformation("Queue endpoint: {QueueEndpoint}", queue.Uri.Obfuscate());
            logger.LogInformation("Table endpoint: {TableEndpoint}", table.Uri.Obfuscate());

            // fetch the user delegation key
            UserDelegationKey? userDelegationKey;
            if (supportsUserDelegationKey)
            {
                userDelegationKey = await blob.GetUserDelegationKeyAsync(startsOn: null, expiresOn: sasExpiry);
            }
            else
            {
                userDelegationKey = null;
            }

            return new ServiceClients(
                created,
                userDelegationKey,
                sasExpiry,
                blob,
                blobClientFactory,
                queue,
                new TableServiceClientWithRetryContext(table, telemetryClient));
        }

        protected virtual HttpPipelineTransport? GetHttpPipelineTransport()
        {
            var httpClient = _httpClientFactory?.Invoke();
            return httpClient != null ? new HttpClientTransport(httpClient) : null;
        }

        private record ServiceClients(
            DateTimeOffset Created,
            UserDelegationKey? UserDelegationKey,
            DateTimeOffset SharedAccessSignatureExpiry,
            BlobServiceClient BlobServiceClient,
            IBlobClientFactory BlobClientFactory,
            QueueServiceClient QueueServiceClient,
            TableServiceClientWithRetryContext TableServiceClientWithRetryContext);
    }
}

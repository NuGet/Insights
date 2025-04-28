// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography.X509Certificates;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Data.Tables;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using Azure.Storage.Sas;
using NuGet.Insights.MemoryStorage;
using NuGet.Insights.StorageNoOpRetry;

#nullable enable

namespace NuGet.Insights
{
    public class ServiceClientFactory
    {
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1);
        private readonly ConcurrentDictionary<StorageSettings, ServiceClients> _serviceClients = new(StorageSettingsComparer.Instance);

        private readonly Func<HttpClient>? _httpClientFactory;
        private readonly MemoryBlobServiceStore _memoryBlobStore;
        private readonly MemoryQueueServiceStore _memoryQueueStore;
        private readonly MemoryTableServiceStore _memoryTableStore;
        private readonly TimeProvider _timeProvider;
        private readonly ITelemetryClient _telemetryClient;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<ServiceClientFactory> _logger;

        public ServiceClientFactory(
            IOptions<NuGetInsightsSettings> options,
            ITelemetryClient telemetryClient,
            ILoggerFactory loggerFactory) : this(
                httpClientFactory: null,
                MemoryBlobServiceStore.SharedStore,
                MemoryQueueServiceStore.SharedStore,
                MemoryTableServiceStore.SharedStore,
                TimeProvider.System,
                options,
                telemetryClient,
                loggerFactory)
        {
        }

        public ServiceClientFactory(
            Func<HttpClient>? httpClientFactory,
            MemoryBlobServiceStore memoryBlobStore,
            MemoryQueueServiceStore memoryQueueStore,
            MemoryTableServiceStore memoryTableStore,
            TimeProvider timeProvider,
            IOptions<NuGetInsightsSettings> options,
            ITelemetryClient telemetryClient,
            ILoggerFactory loggerFactory)
        {
            _httpClientFactory = httpClientFactory;
            _memoryBlobStore = memoryBlobStore;
            _memoryQueueStore = memoryQueueStore;
            _memoryTableStore = memoryTableStore;
            _timeProvider = timeProvider;
            _telemetryClient = telemetryClient;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<ServiceClientFactory>();
        }

        public async Task<QueueServiceClient> GetQueueServiceClientAsync(StorageSettings settings)
        {
            return (await GetCachedServiceClientsAsync(settings)).QueueServiceClient;
        }

        public async Task<BlobServiceClient> GetBlobServiceClientAsync(StorageSettings settings)
        {
            return (await GetCachedServiceClientsAsync(settings)).BlobServiceClient;
        }

        public async Task<BlobClient?> TryGetBlobClientAsync(StorageSettings settings, Uri blobUrl)
        {
            var serviceClients = await GetCachedServiceClientsAsync(settings);

            if (!serviceClients.BlobClientFactory.TryGetBlobClient(serviceClients.BlobServiceClient, blobUrl, out var blobClient))
            {
                return null;
            }

            return blobClient;
        }

        public async Task<Uri> GetBlobReadUrlAsync(StorageSettings settings, string containerName, string blobName)
        {
            var serviceClients = await GetCachedServiceClientsAsync(settings);
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

        public async Task<TableServiceClientWithRetryContext> GetTableServiceClientAsync(StorageSettings settings)
        {
            return (await GetCachedServiceClientsAsync(settings)).TableServiceClientWithRetryContext;
        }

        private async Task<ServiceClients> GetCachedServiceClientsAsync(StorageSettings settings, CancellationToken token = default)
        {
            if (TryGetServiceClients(settings, out var serviceClients))
            {
                return serviceClients;
            }

            await _lock.WaitAsync(token);
            try
            {
                if (TryGetServiceClients(settings, out serviceClients))
                {
                    return serviceClients;
                }

                serviceClients = await GetServiceClientsAsync(
                    created: DateTimeOffset.UtcNow,
                    GetHttpPipelineTransport(),
                    _memoryBlobStore,
                    _memoryQueueStore,
                    _memoryTableStore,
                    _timeProvider,
                    settings,
                    _telemetryClient,
                    _logger,
                    _loggerFactory);
                _serviceClients[settings] = serviceClients;

                return serviceClients;
            }
            finally
            {
                _lock.Release();
            }
        }

        private bool TryGetServiceClients(StorageSettings settings, [NotNullWhen(true)] out ServiceClients? serviceClients)
        {
            if (!_serviceClients.TryGetValue(settings, out serviceClients))
            {
                return false;
            }

            var untilRefresh = GetTimeUntilRefresh(settings, serviceClients.SharedAccessSignatureExpiry, serviceClients.Created);
            if (untilRefresh <= TimeSpan.Zero)
            {
                serviceClients = null;
                return false;
            }

            return true;
        }

        private static TimeSpan GetTimeUntilRefresh(StorageSettings settings, DateTimeOffset sharedAccessSignatureExpiry, DateTimeOffset created)
        {
            // Refresh at half of the SAS duration or the default refresh period, whichever is lesser.
            var sasDuration = sharedAccessSignatureExpiry - created;
            var refreshPeriod = TimeSpan.FromTicks(Math.Min(sasDuration.Ticks / 2, settings.ServiceClientRefreshPeriod.Ticks));
            var sinceCreated = DateTimeOffset.UtcNow - created;
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
            MemoryBlobServiceStore memoryBlobStore,
            MemoryQueueServiceStore memoryQueueStore,
            MemoryTableServiceStore memoryTableStore,
            TimeProvider timeProvider,
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
                    tokenCredential = new DefaultAzureCredential();
                    break;

                case StorageCredentialType.UserAssignedManagedIdentityCredential:
                    tokenCredential = new ManagedIdentityCredential(
                        settings.UserManagedIdentityClientId);
                    break;

                case StorageCredentialType.ClientCertificateCredentialFromKeyVault:
                    var secretReader = new SecretClient(
                        new Uri(settings.StorageClientCertificateKeyVault),
                        new DefaultAzureCredential());
                    KeyVaultSecret certificateContent = await secretReader.GetSecretAsync(
                        settings.StorageClientCertificateKeyVaultCertificateName);
                    var certificateBytes = Convert.FromBase64String(certificateContent.Value);
                    var certificate = new X509Certificate2(certificateBytes);
                    tokenCredential = new ClientCertificateCredential(
                        settings.StorageClientTenantId,
                        settings.StorageClientApplicationId,
                        certificate,
                        new ClientCertificateCredentialOptions { SendCertificateChain = true });
                    break;

                case StorageCredentialType.ClientCertificateCredentialFromPath:
                    tokenCredential = new ClientCertificateCredential(
                        settings.StorageClientTenantId,
                        settings.StorageClientApplicationId,
                        settings.StorageClientCertificatePath,
                        new ClientCertificateCredentialOptions { SendCertificateChain = true });
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

            if (tokenCredential is not null)
            {
                tokenCredential = CachingTokenCredential.MaybeWrap(tokenCredential, loggerFactory, settings, defaultTenantId: null);
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
                blobClientFactory = new MemoryBlobClientFactory(timeProvider, memoryBlobStore);
                queueClientFactory = new MemoryQueueClientFactory(timeProvider, memoryQueueStore);
                tableClientFactory = new MemoryTableClientFactory(timeProvider, memoryTableStore);
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
            var untilRefresh = GetTimeUntilRefresh(settings, sasExpiry, created);
            logger.LogInformation(
                "Using storage account '{StorageAccountName}' with a {CredentialType} credential type. The service clients will be cached for {Duration} hours.",
                blob.AccountName,
                storageCredentialType,
                untilRefresh);

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

        private class StorageSettingsComparer : IEqualityComparer<StorageSettings>
        {
            public static StorageSettingsComparer Instance { get; } = new StorageSettingsComparer();

            public bool Equals(StorageSettings? x, StorageSettings? y)
            {
                if (x is null && y is null)
                {
                    return true;
                }

                if (x is null || y is null)
                {
                    return false;
                }

                return x.StorageAccountName == y.StorageAccountName;
            }

            public int GetHashCode([DisallowNull] StorageSettings obj)
            {
                var hashCode = new HashCode();
                hashCode.Add(obj.StorageAccountName);
                return hashCode.ToHashCode();
            }
        }
    }
}

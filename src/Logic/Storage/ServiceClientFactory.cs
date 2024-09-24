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
using NuGet.Insights.MemoryStorage;
using NuGet.Insights.StorageNoOpRetry;

#nullable enable

namespace NuGet.Insights
{
    public enum StorageCredentialType
    {
        ClientCertificateCredentialFromPath = 1,
        ClientCertificateCredentialFromKeyVault,
        UserAssignedManagedIdentityCredential,
        DefaultAzureCredential,
        StorageAccessKey,
        DevelopmentStorage,
    }

    public partial class ServiceClientFactory
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

            if (serviceClients.StorageCredentialType != StorageCredentialType.DevelopmentStorage
                && (blobUrl.Scheme != "https" // only allow HTTPS
                    || !blobUrl.Host.EndsWith(".blob.core.windows.net", StringComparison.OrdinalIgnoreCase)) // only allow blob storage URLs
                    || !string.IsNullOrEmpty(blobUrl.Query)) // don't allow SAS tokens
            {
                return null;
            }

            BlobClient blobClient;
            if (serviceClients.StorageSharedKeyCredential is not null)
            {
                // don't use a shared key credential if the base URL does not match.
                if (blobUrl.GetLeftPart(UriPartial.Authority) != serviceClients.BlobServiceClient.Uri.GetLeftPart(UriPartial.Authority))
                {
                    return null;
                }

                blobClient = new BlobClient(blobUrl, serviceClients.StorageSharedKeyCredential, serviceClients.BlobClientOptions);
            }
            else
            {
                // token credential requires HTTPS
                if (blobUrl.Scheme != "https")
                {
                    return null;
                }

                blobClient = new BlobClient(blobUrl, serviceClients.StorageTokenCredential!, serviceClients.BlobClientOptions);
            }

            if (serviceClients.UseMemoryStorage)
            {
                var serviceClient = (MemoryBlobServiceClient)serviceClients.BlobServiceClient;
                blobClient = serviceClient
                    .GetBlobContainerClient(blobClient.BlobContainerName)
                    .GetBlobClient(blobClient.Name);
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

        public static StorageCredentialType GetStorageCredentialType(StorageSettings settings)
        {
            if (settings.UseDevelopmentStorage)
            {
                return StorageCredentialType.DevelopmentStorage;
            }
            else if (settings.StorageAccessKey is not null)
            {
                return StorageCredentialType.StorageAccessKey;
            }
            else if (settings.StorageAccountName is not null)
            {
                if (settings.StorageClientCertificatePath is not null)
                {
                    return StorageCredentialType.ClientCertificateCredentialFromPath;
                }
                else if (settings.StorageClientCertificateKeyVault is not null)
                {
                    return StorageCredentialType.ClientCertificateCredentialFromKeyVault;
                }
                else if (settings.UserManagedIdentityClientId is not null)
                {
                    return StorageCredentialType.UserAssignedManagedIdentityCredential;
                }
                else
                {
                    return StorageCredentialType.DefaultAzureCredential;
                }
            }
            else
            {
                throw new ArgumentException(
                    $"Either the {nameof(settings.StorageAccountName)} property must be set or the {nameof(settings.UseDevelopmentStorage)} must be set to true. " +
                    $"Set {nameof(settings.UseDevelopmentStorage)} to true if you want to use Azurite.");
            }
        }

        private static async Task<ServiceClients> GetServiceClientsAsync(
            DateTimeOffset created,
            HttpPipelineTransport? httpPipelineTransport,
            StorageSettings settings,
            ITelemetryClient telemetryClient,
            ILogger logger,
            ILoggerFactory loggerFactory)
        {
            var sasExpiry = created.Add(settings.ServiceClientSasDuration);
            var useMemoryStorage = settings.UseMemoryStorage;

            TokenCredential? tokenCredential = null;
            StorageSharedKeyCredential? storageAccessKeyCredential = null;
            TableSharedKeyCredential? tableAccessKeyCredential = null;

            var storageCredentialType = GetStorageCredentialType(settings);
            string? accountName = settings.StorageAccountName;
            switch (storageCredentialType)
            {
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

                case StorageCredentialType.ClientCertificateCredentialFromKeyVault:
                    tokenCredential = await CredentialCache.GetLazyClientCertificateCredentialTask(
                        settings.StorageClientTenantId,
                        settings.StorageClientApplicationId,
                        () => CredentialCache.GetLazyCertificateTask(
                            settings.StorageClientCertificateKeyVault,
                            settings.StorageClientCertificateKeyVaultCertificateName).Value).Value;
                    break;

                case StorageCredentialType.UserAssignedManagedIdentityCredential:
                    tokenCredential = new ManagedIdentityCredential(
                        settings.UserManagedIdentityClientId);
                    break;

                case StorageCredentialType.DefaultAzureCredential:
                    tokenCredential = CredentialCache.DefaultAzureCredential;
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
                    storageAccessKeyCredential = DevelopmentStorage.StorageSharedKeyCredential;
                    tableAccessKeyCredential = DevelopmentStorage.TableSharedKeyCredential;
                    break;

                default:
                    throw new NotImplementedException();
            }

            var blobClientOptions = GetOptions(settings, loggerFactory, new BlobClientOptions(), httpPipelineTransport);
            var queueClientOptions = GetOptions(settings, loggerFactory, new QueueClientOptions(), httpPipelineTransport);
            var tableClientOptions = GetOptions(settings, loggerFactory, new TableClientOptions(), httpPipelineTransport);

            var (blobServiceUri, queueServiceUri, tableServiceUri) = storageCredentialType == StorageCredentialType.DevelopmentStorage
                ? DevelopmentStorage.GetStorageEndpoints()
                : StorageUtility.GetStorageEndpoints(accountName!);

            BlobServiceClient blob;
            QueueServiceClient queue;
            TableServiceClient table;
            UserDelegationKey? userDelegationKey;
            if (tokenCredential is not null)
            {
                if (useMemoryStorage)
                {
                    blob = new MemoryBlobServiceClient(blobServiceUri, tokenCredential, blobClientOptions);
                }
                else
                {
                    blob = new BlobServiceClient(blobServiceUri, tokenCredential, blobClientOptions);
                }

                queue = new QueueServiceClient(queueServiceUri, tokenCredential, queueClientOptions);
                table = new TableServiceClient(tableServiceUri, tokenCredential, tableClientOptions);
                userDelegationKey = await blob.GetUserDelegationKeyAsync(startsOn: null, expiresOn: sasExpiry);

                logger.LogInformation(
                    "Using {PersistenceType} storage account '{StorageAccountName}' with a {CredentialType} and a user delegation key expiring at {Expiry:O}, which is in {RemainingHours:F2} hours.",
                    useMemoryStorage ? "in-memory" : "external",
                    blob.AccountName,
                    storageCredentialType,
                    sasExpiry,
                    (sasExpiry - created).TotalHours);
            }
            else
            {
                if (useMemoryStorage)
                {
                    blob = new MemoryBlobServiceClient(blobServiceUri, storageAccessKeyCredential!, blobClientOptions);
                }
                else
                {
                    blob = new BlobServiceClient(blobServiceUri, storageAccessKeyCredential, blobClientOptions);
                }

                queue = new QueueServiceClient(queueServiceUri, storageAccessKeyCredential, queueClientOptions);
                table = new TableServiceClient(tableServiceUri, tableAccessKeyCredential, tableClientOptions);
                userDelegationKey = null;

                logger.LogInformation(
                    "Using {PersistenceType} storage account '{StorageAccountName}' with a {CredentialType}.",
                    useMemoryStorage ? "in-memory" : "external",
                    blob.AccountName,
                    storageCredentialType);
            }

            logger.LogInformation("Blob endpoint: {BlobEndpoint}", blob.Uri.Obfuscate());
            logger.LogInformation("Queue endpoint: {QueueEndpoint}", queue.Uri.Obfuscate());
            logger.LogInformation("Table endpoint: {TableEndpoint}", table.Uri.Obfuscate());

            return new ServiceClients(
                created,
                storageCredentialType,
                tokenCredential,
                storageAccessKeyCredential,
                tableAccessKeyCredential,
                userDelegationKey,
                sasExpiry,
                blob,
                blobClientOptions,
                queue,
                table,
                new TableServiceClientWithRetryContext(table, telemetryClient),
                useMemoryStorage);
        }

        protected virtual HttpPipelineTransport? GetHttpPipelineTransport()
        {
            var httpClient = _httpClientFactory?.Invoke();
            return httpClient != null ? new HttpClientTransport(httpClient) : null;
        }

        private record ServiceClients(
            DateTimeOffset Created,
            StorageCredentialType StorageCredentialType,
            TokenCredential? StorageTokenCredential,
            StorageSharedKeyCredential? StorageSharedKeyCredential,
            TableSharedKeyCredential? TableSharedKeyCredential,
            UserDelegationKey? UserDelegationKey,
            DateTimeOffset SharedAccessSignatureExpiry,
            BlobServiceClient BlobServiceClient,
            BlobClientOptions BlobClientOptions,
            QueueServiceClient QueueServiceClient,
            TableServiceClient TableServiceClient,
            TableServiceClientWithRetryContext TableServiceClientWithRetryContext,
            bool UseMemoryStorage);
    }
}

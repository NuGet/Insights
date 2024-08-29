// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Data.Tables;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using Azure.Storage.Sas;
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
        ConnectionStringWithStorageAccountKey,
        ConnectionStringWithSharedAccessSignature,
        UseDevelopmentStorage,
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

        public async Task<BlobClient> GetBlobClientAsync(Uri blobUrl)
        {
            var serviceClients = await GetCachedServiceClientsAsync();
            if (serviceClients.StorageTokenCredential is null)
            {
                throw new ArgumentNullException("A blob client can only be created by URL when a token credential is used.");
            }

            return new BlobClient(blobUrl, serviceClients.StorageTokenCredential, serviceClients.BlobClientOptions);
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

        public async Task<StorageCredentialType> GetStorageCredentialTypeAsync()
        {
            return (await GetCachedServiceClientsAsync()).StorageCredentialType;
        }

        public async Task<TokenCredential?> GetStorageTokenCredentialAsync()
        {
            return (await GetCachedServiceClientsAsync()).StorageTokenCredential;
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

        private T GetOptions<T>(T options, HttpPipelineTransport? transport) where T : ClientOptions
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

        public static StorageCredentialType GetStorageCredentialType(NuGetInsightsSettings settings)
        {
            if (settings.StorageConnectionString is not null)
            {
                var match = ConnectionStringCredentialRegex().Match(settings.StorageConnectionString);
                if (match.Success)
                {
                    var key = match.Groups["Key"].ValueSpan;
                    var value = match.Groups["Value"].ValueSpan;
                    if (key.Equals("AccountKey", StringComparison.OrdinalIgnoreCase))
                    {
                        return StorageCredentialType.ConnectionStringWithStorageAccountKey;
                    }
                    else if (key.Equals("SharedAccessSignature", StringComparison.OrdinalIgnoreCase))
                    {
                        return StorageCredentialType.ConnectionStringWithSharedAccessSignature;
                    }
                    else if (key.Equals("UseDevelopmentStorage", StringComparison.OrdinalIgnoreCase) && value.Equals("true", StringComparison.OrdinalIgnoreCase))
                    {
                        return StorageCredentialType.UseDevelopmentStorage;
                    }
                }

                throw new ArgumentException($"The {nameof(settings.StorageConnectionString)} must either have an AccountKey or SharedAccessSignature setting.");
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
                    $"Either the {nameof(settings.StorageConnectionString)} property or the {nameof(settings.StorageAccountName)} must be set. " +
                    $"Set {nameof(settings.StorageConnectionString)} to '{StorageUtility.EmulatorConnectionString}' if you want to use Azurite.");
            }
        }

        private async Task<ServiceClients> GetServiceClientsAsync(DateTimeOffset created)
        {
            var httpPipelineTransport = GetHttpPipelineTransport();

            BlobServiceClient blob;
            BlobClientOptions blobClientOptions;
            QueueServiceClient queue;
            TableServiceClient table;
            TokenCredential? tokenCredential;
            UserDelegationKey? userDelegationKey;
            DateTimeOffset sasExpiry = DateTimeOffset.UtcNow.Add(_options.Value.ServiceClientSasDuration);

            var storageCredentialType = GetStorageCredentialType(_options.Value);
            switch (storageCredentialType)
            {
                case StorageCredentialType.ClientCertificateCredentialFromPath:
                    tokenCredential = await CredentialCache.GetLazyClientCertificateCredentialTask(
                        _options.Value.StorageClientTenantId,
                        _options.Value.StorageClientApplicationId,
                        () =>
                        {
                            var certificate = new X509Certificate2(_options.Value.StorageClientCertificatePath);
                            return Task.FromResult(certificate);
                        }).Value;
                    break;
                case StorageCredentialType.ClientCertificateCredentialFromKeyVault:
                    tokenCredential = await CredentialCache.GetLazyClientCertificateCredentialTask(
                        _options.Value.StorageClientTenantId,
                        _options.Value.StorageClientApplicationId,
                        () => CredentialCache.GetLazyCertificateTask(
                            _options.Value.StorageClientCertificateKeyVault,
                            _options.Value.StorageClientCertificateKeyVaultCertificateName).Value).Value;
                    break;
                case StorageCredentialType.UserAssignedManagedIdentityCredential:
                    tokenCredential = new ManagedIdentityCredential(
                        _options.Value.UserManagedIdentityClientId);
                    break;
                case StorageCredentialType.DefaultAzureCredential:
                    tokenCredential = CredentialCache.DefaultAzureCredential;
                    break;
                case StorageCredentialType.ConnectionStringWithStorageAccountKey:
                case StorageCredentialType.ConnectionStringWithSharedAccessSignature:
                case StorageCredentialType.UseDevelopmentStorage:
                    tokenCredential = null;
                    break;
                default:
                    throw new NotImplementedException();
            }

            blobClientOptions = GetOptions(new BlobClientOptions(), httpPipelineTransport);

            if (tokenCredential is not null)
            {
                blob = new BlobServiceClient(
                    new Uri($"https://{_options.Value.StorageAccountName}.blob.core.windows.net"),
                    tokenCredential,
                    blobClientOptions);

                queue = new QueueServiceClient(
                    new Uri($"https://{_options.Value.StorageAccountName}.queue.core.windows.net"),
                    tokenCredential,
                    GetOptions(new QueueClientOptions(), httpPipelineTransport));

                table = new TableServiceClient(
                    new Uri($"https://{_options.Value.StorageAccountName}.table.core.windows.net"),
                    tokenCredential,
                    GetOptions(new TableClientOptions(), httpPipelineTransport));

                _logger.LogInformation(
                    "Using storage account '{StorageAccountName}' with a {CredentialType} and a user delegation key expiring at {Expiry:O}, which is in {RemainingHours:F2} hours.",
                    _options.Value.StorageAccountName,
                    storageCredentialType,
                    sasExpiry,
                    (sasExpiry - DateTimeOffset.UtcNow).TotalHours);

                userDelegationKey = await blob.GetUserDelegationKeyAsync(startsOn: null, expiresOn: sasExpiry);
            }
            else
            {
                _logger.LogInformation("Using the configured storage connection string.");

                blob = new BlobServiceClient(
                    _options.Value.StorageConnectionString,
                    blobClientOptions);

                queue = new QueueServiceClient(
                    _options.Value.StorageConnectionString,
                    GetOptions(new QueueClientOptions(), httpPipelineTransport));

                table = new TableServiceClient(
                    _options.Value.StorageConnectionString,
                    GetOptions(new TableClientOptions(), httpPipelineTransport));

                var canGenerateAccountSasUri = storageCredentialType == StorageCredentialType.ConnectionStringWithStorageAccountKey
                    || storageCredentialType == StorageCredentialType.UseDevelopmentStorage;
                if (canGenerateAccountSasUri != blob.CanGenerateAccountSasUri)
                {
                    throw new InvalidOperationException("The storage connection string appears to have an storage account key but cannot generate account SAS.");
                }

                userDelegationKey = null;
            }

            _logger.LogInformation("Blob endpoint: {BlobEndpoint}", blob.Uri.Obfuscate());
            _logger.LogInformation("Queue endpoint: {QueueEndpoint}", queue.Uri.Obfuscate());
            _logger.LogInformation("Table endpoint: {TableEndpoint}", table.Uri.Obfuscate());

            return new ServiceClients(
                created,
                storageCredentialType,
                tokenCredential,
                userDelegationKey,
                sasExpiry,
                blob,
                blobClientOptions,
                queue,
                table,
                new TableServiceClientWithRetryContext(table, _telemetryClient));
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
            UserDelegationKey? UserDelegationKey,
            DateTimeOffset SharedAccessSignatureExpiry,
            BlobServiceClient BlobServiceClient,
            BlobClientOptions BlobClientOptions,
            QueueServiceClient QueueServiceClient,
            TableServiceClient TableServiceClient,
            TableServiceClientWithRetryContext TableServiceClientWithRetryContext);

        [GeneratedRegex(
            "(^|;)\\s*(?<Key>AccountKey|SharedAccessSignature|UseDevelopmentStorage)\\s*=\\s*(?<Value>[^\\s;]+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
        private static partial Regex ConnectionStringCredentialRegex();
    }
}

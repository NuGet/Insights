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
        private readonly ConcurrentDictionary<StorageSettings, ServiceClients> _serviceClients = new(StorageSettingsComparer.Instance);
        private readonly Func<HttpClient>? _httpClientFactory;
        private readonly ITelemetryClient _telemetryClient;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<ServiceClientFactory> _logger;

        public ServiceClientFactory(
            ITelemetryClient telemetryClient,
            ILoggerFactory loggerFactory) : this(httpClientFactory: null, telemetryClient, loggerFactory)
        {
        }

        public ServiceClientFactory(
            Func<HttpClient>? httpClientFactory,
            ITelemetryClient telemetryClient,
            ILoggerFactory loggerFactory)
        {
            _httpClientFactory = httpClientFactory;
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

        public async Task<BlobClient> GetBlobClientAsync(StorageSettings settings, Uri blobUrl)
        {
            var serviceClients = await GetCachedServiceClientsAsync(settings);
            if (serviceClients.StorageTokenCredential is null)
            {
                throw new ArgumentNullException("A blob client can only be created by URL when a token credential is used.");
            }

            return new BlobClient(blobUrl, serviceClients.StorageTokenCredential, serviceClients.BlobClientOptions);
        }

        public async Task<Uri> GetBlobReadUrlAsync(StorageSettings settings, string containerName, string blobName)
        {
            var serviceClients = await GetCachedServiceClientsAsync(settings);
            var blobClient = serviceClients
                .BlobServiceClient
                .GetBlobContainerClient(containerName)
                .GetBlobClient(blobName);

            if (settings.StorageBlobReadSharedAccessSignature != null)
            {
                return new UriBuilder(blobClient.Uri) { Query = settings.StorageBlobReadSharedAccessSignature }.Uri;
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

        public async Task<TableServiceClientWithRetryContext> GetTableServiceClientAsync(StorageSettings settings)
        {
            return (await GetCachedServiceClientsAsync(settings)).TableServiceClientWithRetryContext;
        }

        public async Task<StorageCredentialType> GetStorageCredentialTypeAsync(StorageSettings settings)
        {
            return (await GetCachedServiceClientsAsync(settings)).StorageCredentialType;
        }

        public async Task<TokenCredential?> GetStorageTokenCredentialAsync(StorageSettings settings)
        {
            return (await GetCachedServiceClientsAsync(settings)).StorageTokenCredential;
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

                serviceClients = await GetServiceClientsAsync(settings, created: DateTimeOffset.UtcNow);
                _serviceClients.TryUpdate(settings, serviceClients, serviceClients);
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

            var untilRefresh = GetTimeUntilRefresh(settings, serviceClients);
            if (untilRefresh <= TimeSpan.Zero)
            {
                serviceClients = null;
                return false;
            }

            return true;
        }

        private TimeSpan GetTimeUntilRefresh(StorageSettings settings, ServiceClients serviceClients)
        {
            // Refresh at half of the SAS duration or the default refresh period, whichever is lesser.
            var sasDuration = serviceClients.SharedAccessSignatureExpiry - serviceClients.Created;
            var refreshPeriod = TimeSpan.FromTicks(Math.Min(sasDuration.Ticks / 2, settings.ServiceClientRefreshPeriod.Ticks));
            var sinceCreated = DateTimeOffset.UtcNow - serviceClients.Created;
            var untilRefresh = refreshPeriod - sinceCreated;

            return untilRefresh > TimeSpan.Zero ? untilRefresh : TimeSpan.Zero;
        }

        private T GetOptions<T>(StorageSettings settings, T options, HttpPipelineTransport? transport) where T : ClientOptions
        {
            if (transport is not null)
            {
                options.Transport = transport;
            }

            var maxRetries = settings.AzureServiceClientMaxRetries;
            options.Retry.MaxRetries = maxRetries;
            options.Retry.NetworkTimeout = settings.AzureServiceClientNetworkTimeout;
            options.RetryPolicy = new StorageNoOpRetryPolicy(
                _loggerFactory.CreateLogger<StorageNoOpRetryPolicy>(),
                maxRetries);

            return options;
        }

        public static StorageCredentialType GetStorageCredentialType(StorageSettings settings)
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

        private async Task<ServiceClients> GetServiceClientsAsync(StorageSettings settings, DateTimeOffset created)
        {
            var httpPipelineTransport = GetHttpPipelineTransport();

            BlobServiceClient blob;
            BlobClientOptions blobClientOptions;
            QueueServiceClient queue;
            TableServiceClient table;
            TokenCredential? tokenCredential;
            UserDelegationKey? userDelegationKey;
            DateTimeOffset sasExpiry = DateTimeOffset.UtcNow.Add(settings.ServiceClientSasDuration);

            var storageCredentialType = GetStorageCredentialType(settings);
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
                case StorageCredentialType.ConnectionStringWithStorageAccountKey:
                case StorageCredentialType.ConnectionStringWithSharedAccessSignature:
                case StorageCredentialType.UseDevelopmentStorage:
                    tokenCredential = null;
                    break;
                default:
                    throw new NotImplementedException();
            }

            blobClientOptions = GetOptions(settings, new BlobClientOptions(), httpPipelineTransport);

            if (tokenCredential is not null)
            {
                blob = new BlobServiceClient(
                    new Uri($"https://{settings.StorageAccountName}.blob.core.windows.net"),
                    tokenCredential,
                    blobClientOptions);

                queue = new QueueServiceClient(
                    new Uri($"https://{settings.StorageAccountName}.queue.core.windows.net"),
                    tokenCredential,
                    GetOptions(settings, new QueueClientOptions(), httpPipelineTransport));

                table = new TableServiceClient(
                    new Uri($"https://{settings.StorageAccountName}.table.core.windows.net"),
                    tokenCredential,
                    GetOptions(settings, new TableClientOptions(), httpPipelineTransport));

                _logger.LogInformation(
                    "Using storage account '{StorageAccountName}' with a {CredentialType} and a user delegation key expiring at {Expiry:O}, which is in {RemainingHours:F2} hours.",
                    settings.StorageAccountName,
                    storageCredentialType,
                    sasExpiry,
                    (sasExpiry - DateTimeOffset.UtcNow).TotalHours);

                userDelegationKey = await blob.GetUserDelegationKeyAsync(startsOn: null, expiresOn: sasExpiry);
            }
            else
            {
                _logger.LogInformation("Using the configured storage connection string.");

                blob = new BlobServiceClient(
                    settings.StorageConnectionString,
                    blobClientOptions);

                queue = new QueueServiceClient(
                    settings.StorageConnectionString,
                    GetOptions(settings, new QueueClientOptions(), httpPipelineTransport));

                table = new TableServiceClient(
                    settings.StorageConnectionString,
                    GetOptions(settings, new TableClientOptions(), httpPipelineTransport));

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

                return x.StorageAccountName == y.StorageAccountName
                    && x.StorageConnectionString == y.StorageConnectionString;
            }

            public int GetHashCode([DisallowNull] StorageSettings obj)
            {
                var hashCode = new HashCode();
                hashCode.Add(obj.StorageAccountName);
                hashCode.Add(obj.StorageConnectionString);
                return hashCode.ToHashCode();
            }
        }
    }
}

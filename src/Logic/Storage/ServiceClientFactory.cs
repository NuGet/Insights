// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core.Pipeline;
using Azure.Data.Tables;
using Azure.Identity;
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

            // Refresh at half of the SAS duration.
            var originalDuration = serviceClients.SharedAccessSignatureExpiry - serviceClients.Created;
            var halfUntilExpiry = serviceClients.Created + (originalDuration / 2);
            var untilRefresh = halfUntilExpiry - DateTimeOffset.UtcNow;

            return untilRefresh > TimeSpan.Zero ? untilRefresh : TimeSpan.Zero;
        }

        private async Task<ServiceClients> GetServiceClientsAsync(DateTimeOffset created)
        {
            var httpPipelineTransport = GetHttpPipelineTransport();

            BlobServiceClient blob;
            QueueServiceClient queue;
            TableServiceClient table;
            UserDelegationKey userDelegationKey;
            DateTimeOffset sasExpiry = DateTimeOffset.UtcNow.Add(2 * DefaultRefreshPeriod);

            if (_options.Value.StorageAccountName != null)
            {
                blob = new BlobServiceClient(
                    new Uri($"https://{_options.Value.StorageAccountName}.blob.core.windows.net"),
                    new DefaultAzureCredential(),
                    options: httpPipelineTransport != null ? new BlobClientOptions { Transport = httpPipelineTransport } : null);

                queue = new QueueServiceClient(
                    new Uri($"https://{_options.Value.StorageAccountName}.queue.core.windows.net"),
                    new DefaultAzureCredential(),
                    options: httpPipelineTransport != null ? new QueueClientOptions { Transport = httpPipelineTransport } : null);

                table = new TableServiceClient(
                    new Uri($"https://{_options.Value.StorageAccountName}.table.core.windows.net"),
                    new DefaultAzureCredential(),
                    options: httpPipelineTransport != null ? new TableClientOptions { Transport = httpPipelineTransport } : null);

                userDelegationKey = await blob.GetUserDelegationKeyAsync(startsOn: null, expiresOn: sasExpiry);

                _logger.LogInformation(
                    "Using storage account '{StorageAccountName}' and a user delegation key expiring at {Expiry:O}, which is in {RemainingHours:F2} hours.",
                    _options.Value.StorageAccountName,
                    sasExpiry,
                    (sasExpiry - DateTimeOffset.UtcNow).TotalHours);
            }
            else
            {
                _logger.LogInformation("Using the configured storage connection string.");

                blob = new BlobServiceClient(
                    _options.Value.StorageConnectionString,
                    options: httpPipelineTransport != null ? new BlobClientOptions { Transport = httpPipelineTransport } : null);

                queue = new QueueServiceClient(
                    _options.Value.StorageConnectionString,
                    options: httpPipelineTransport != null ? new QueueClientOptions { Transport = httpPipelineTransport } : null);

                table = new TableServiceClient(
                    _options.Value.StorageConnectionString,
                    options: httpPipelineTransport != null ? new TableClientOptions { Transport = httpPipelineTransport } : null);

                userDelegationKey = null;
            }

            _logger.LogInformation("Blob endpoint: {BlobEndpoint}", blob.Uri);
            _logger.LogInformation("Queue endpoint: {QueueEndpoint}", queue.Uri);
            // No Uri property for TableServiceClient, see https://github.com/Azure/azure-sdk-for-net/issues/19881

            return new ServiceClients(
                created,
                userDelegationKey,
                sasExpiry,
                blob,
                queue,
                table);
        }

        protected virtual HttpPipelineTransport GetHttpPipelineTransport()
        {
            return null;
        }

        private record ServiceClients(
            DateTimeOffset Created,
            UserDelegationKey UserDelegationKey,
            DateTimeOffset SharedAccessSignatureExpiry,
            BlobServiceClient BlobServiceClient,
            QueueServiceClient QueueServiceClient,
            TableServiceClient TableServiceClient);
    }
}

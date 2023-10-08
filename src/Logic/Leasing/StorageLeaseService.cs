// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Options;

namespace NuGet.Insights
{
    public class StorageLeaseService
    {
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IOptions<NuGetInsightsSettings> _options;

        public StorageLeaseService(
            ServiceClientFactory serviceClientFactory, IOptions<NuGetInsightsSettings> options)
        {
            _serviceClientFactory = serviceClientFactory;
            _options = options;
        }

        public async Task InitializeAsync()
        {
            await (await GetContainerAsync()).CreateIfNotExistsAsync(retry: true);
        }

        public async Task<StorageLeaseResult> AcquireAsync(string name, TimeSpan leaseDuration)
        {
            return await TryAcquireAsync(name, leaseDuration, shouldThrow: true);
        }

        public async Task<StorageLeaseResult> TryAcquireAsync(string name, TimeSpan leaseDuration)
        {
            return await TryAcquireAsync(name, leaseDuration, shouldThrow: false);
        }

        private async Task<StorageLeaseResult> TryAcquireAsync(string name, TimeSpan leaseDuration, bool shouldThrow)
        {
            var blob = await GetBlobAsync(name);
            var leaseClient = blob.GetBlobLeaseClient();

            BlobLease lease;
            try
            {
                try
                {
                    lease = await leaseClient.AcquireAsync(leaseDuration);
                }
                catch (RequestFailedException acquireEx) when (acquireEx.Status == (int)HttpStatusCode.NotFound)
                {
                    try
                    {
                        await blob.UploadAsync(Stream.Null, overwrite: false);
                    }
                    catch (RequestFailedException createEx) when (createEx.Status == (int)HttpStatusCode.Conflict || createEx.Status == (int)HttpStatusCode.PreconditionFailed)
                    {
                        // Ignore this exception. Another thread created the blob already.
                    }

                    lease = await leaseClient.AcquireAsync(leaseDuration);
                }
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict)
            {
                if (shouldThrow)
                {
                    throw new InvalidOperationException(StorageLeaseResult.NotAvailable, ex);
                }
                else
                {
                    return StorageLeaseResult.NotLeased(name);
                }
            }

            // Update the etag so we can detect if anyone else has acquired the lease successfully. This also
            // updates the Last-Modified date which prevents a blob life cycle policy from deleting this blob.
            try
            {
                BlobInfo blobInfo = await blob.SetMetadataAsync(
                    new Dictionary<string, string> { { "leasestarted", DateTimeOffset.UtcNow.ToString("O") } },
                    new BlobRequestConditions { LeaseId = lease.LeaseId });

                return StorageLeaseResult.Leased(name, lease.LeaseId, blobInfo.ETag);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.PreconditionFailed)
            {
                if (shouldThrow)
                {
                    throw new InvalidOperationException(StorageLeaseResult.NotAvailable, ex);
                }
                else
                {
                    return StorageLeaseResult.NotLeased(name);
                }
            }
        }

        public async Task RenewAsync(StorageLeaseResult result)
        {
            await TryRenewAsync(result, shouldThrow: true);
        }

        public async Task<bool> TryRenewAsync(StorageLeaseResult result)
        {
            return await TryRenewAsync(result, shouldThrow: false);
        }

        private async Task<bool> TryRenewAsync(StorageLeaseResult result, bool shouldThrow)
        {
            var blob = await GetBlobAsync(result.Name);
            var leaseClient = blob.GetBlobLeaseClient(result.Lease);

            try
            {
                await leaseClient.RenewAsync();
                return true;
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict)
            {
                if (shouldThrow)
                {
                    throw new InvalidOperationException(StorageLeaseResult.AcquiredBySomeoneElse, ex);
                }
                else
                {
                    return false;
                }
            }
        }

        public async Task BreakAsync(string name)
        {
            var blob = await GetBlobAsync(name);
            var leaseClient = blob.GetBlobLeaseClient();
            await leaseClient.BreakAsync(breakPeriod: TimeSpan.Zero);
        }

        public async Task<bool> TryReleaseAsync(StorageLeaseResult result)
        {
            return await TryReleaseAsync(result, shouldThrow: false);
        }

        public async Task ReleaseAsync(StorageLeaseResult result)
        {
            await TryReleaseAsync(result, shouldThrow: true);
        }

        private async Task<bool> TryReleaseAsync(StorageLeaseResult result, bool shouldThrow)
        {
            var blob = await GetBlobAsync(result.Name);
            var leaseClient = blob.GetBlobLeaseClient(result.Lease);

            try
            {
                await leaseClient.ReleaseAsync();
                return true;
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict)
            {
                try
                {
                    BlobProperties properties = await blob.GetPropertiesAsync();
                    if (properties.ETag == result.ETag && properties.LeaseState == LeaseState.Available)
                    {
                        return true;
                    }
                }
                catch
                {
                    // Ignore, this is best effort.
                }

                if (shouldThrow)
                {
                    throw new InvalidOperationException(StorageLeaseResult.AcquiredBySomeoneElse, ex);
                }
                else
                {
                    return false;
                }
            }
        }

        private async Task<BlobClient> GetBlobAsync(string name)
        {
            return (await GetContainerAsync())
                .GetBlobClient(name);
        }

        private async Task<BlobContainerClient> GetContainerAsync()
        {
            return (await _serviceClientFactory.GetBlobServiceClientAsync())
                .GetBlobContainerClient(_options.Value.LeaseContainerName);
        }
    }
}

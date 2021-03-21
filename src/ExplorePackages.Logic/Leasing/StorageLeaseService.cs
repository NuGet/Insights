using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages
{
    public class StorageLeaseService
    {
        private readonly NewServiceClientFactory _serviceClientFactory;
        private readonly IOptions<ExplorePackagesSettings> _options;

        public StorageLeaseService(
            NewServiceClientFactory serviceClientFactory, IOptions<ExplorePackagesSettings> options)
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

            if (!await blob.ExistsAsync())
            {
                try
                {
                    await blob.UploadAsync(Stream.Null, overwrite: false);
                }
                catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict)
                {
                    // Ignore this exception.
                }
            }

            try
            {
                BlobLease lease = await leaseClient.AcquireAsync(leaseDuration);
                return StorageLeaseResult.Leased(name, lease.LeaseId);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict)
            {
                if (shouldThrow)
                {
                    throw new InvalidOperationException(StorageLeaseResult.NotAvailable, ex);
                }
                else
                {
                    return StorageLeaseResult.NotLeased();
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

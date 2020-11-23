using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Knapcode.ExplorePackages.Logic
{
    public class StorageLeaseService
    {
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IOptionsSnapshot<ExplorePackagesSettings> _options;

        public StorageLeaseService(ServiceClientFactory serviceClientFactory, IOptionsSnapshot<ExplorePackagesSettings> options)
        {
            _serviceClientFactory = serviceClientFactory;
            _options = options;
        }

        public async Task InitializeAsync()
        {
            await GetContainer().CreateIfNotExistsAsync(retry: true);
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
            var blob = GetBlob(name);

            if (!await blob.ExistsAsync())
            {
                try
                {
                    await blob.UploadTextAsync(
                        string.Empty,
                        Encoding.ASCII,
                        AccessCondition.GenerateIfNotExistsCondition(),
                        options: null,
                        operationContext: null);
                }
                catch (StorageException ex) when (ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.PreconditionFailed)
                {
                    // Ignore this exception.
                }
            }

            try
            {
                var leaseId = await blob.AcquireLeaseAsync(leaseDuration);
                return StorageLeaseResult.Leased(name, leaseId);
            }
            catch (StorageException ex) when (ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.Conflict)
            {
                if (shouldThrow)
                {
                    throw new InvalidOperationException(DatabaseLeaseService.NotAvailable, ex);
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
            var blob = GetBlob(result.Name);

            try
            {
                await blob.RenewLeaseAsync(AccessCondition.GenerateLeaseCondition(result.Lease));
                return true;
            }
            catch (StorageException ex) when (ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.Conflict)
            {
                if (shouldThrow)
                {
                    throw new InvalidOperationException(DatabaseLeaseService.AcquiredBySomeoneElse, ex);
                }
                else
                {
                    return false;
                }
            }
        }

        public async Task BreakAsync(string name)
        {
            var blob = GetBlob(name);
            await blob.BreakLeaseAsync(TimeSpan.Zero);
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
            var blob = GetBlob(result.Name);

            try
            {
                await blob.ReleaseLeaseAsync(AccessCondition.GenerateLeaseCondition(result.Lease));
                return true;
            }
            catch (StorageException ex) when (ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.Conflict)
            {
                if (shouldThrow)
                {
                    throw new InvalidOperationException(DatabaseLeaseService.AcquiredBySomeoneElse, ex);
                }
                else
                {
                    return false;
                }
            }
        }

        private CloudBlockBlob GetBlob(string name)
        {
            return GetContainer().GetBlockBlobReference(name);
        }

        private CloudBlobContainer GetContainer()
        {
            return _serviceClientFactory
                .GetStorageAccount()
                .CreateCloudBlobClient()
                .GetContainerReference(_options.Value.LeaseContainerName);
        }
    }
}

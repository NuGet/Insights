using System;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;

namespace Knapcode.ExplorePackages.Logic
{
    public class ServiceClientFactory
    {
        private readonly Lazy<CloudStorageAccount> _storageAccount;
        private readonly Lazy<CloudStorageAccount> _latestPackageLeavesStorageAccount;

        public ServiceClientFactory(IOptionsSnapshot<ExplorePackagesSettings> options)
        {
            _storageAccount = GetLazyStorageAccount(options.Value.StorageConnectionString);
            _latestPackageLeavesStorageAccount = GetLazyStorageAccount(options.Value.LatestPackageLeavesStorageConnectionString);
        }

        public CloudStorageAccount GetStorageAccount() => _storageAccount.Value;
        public CloudStorageAccount GetLatestPackageLeavesStorageAccount() => _latestPackageLeavesStorageAccount.Value;

        private static Lazy<CloudStorageAccount> GetLazyStorageAccount(string connectionString)
        {
            return new Lazy<CloudStorageAccount>(() => CloudStorageAccount.Parse(connectionString));
        }
    }
}

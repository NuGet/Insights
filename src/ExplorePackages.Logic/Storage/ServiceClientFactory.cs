using System;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;

namespace Knapcode.ExplorePackages.Logic
{
    public class ServiceClientFactory
    {
        private readonly Lazy<CloudStorageAccount> _storageAccount;

        public ServiceClientFactory(IOptionsSnapshot<ExplorePackagesSettings> options)
        {
            _storageAccount = GetLazyStorageAccount(options.Value.StorageConnectionString);
        }

        public CloudStorageAccount GetStorageAccount() => _storageAccount.Value;

        private static Lazy<CloudStorageAccount> GetLazyStorageAccount(string connectionString)
        {
            return new Lazy<CloudStorageAccount>(() => CloudStorageAccount.Parse(connectionString));
        }
    }
}

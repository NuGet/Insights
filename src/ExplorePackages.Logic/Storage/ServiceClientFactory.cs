using System;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;

namespace Knapcode.ExplorePackages
{
    public class ServiceClientFactory : IServiceClientFactory
    {
        private readonly Lazy<CloudStorageAccount> _storageAccount;
        private readonly Lazy<ICloudStorageAccount> _abstractedStorageAccount;

        public ServiceClientFactory(IOptionsSnapshot<ExplorePackagesSettings> options)
        {
            _storageAccount = GetLazyStorageAccount(options.Value.StorageConnectionString);
            _abstractedStorageAccount = GetLazyAbstractedStorageAccount(_storageAccount);
        }

        public CloudStorageAccount GetStorageAccount() => _storageAccount.Value;
        public ICloudStorageAccount GetAbstractedStorageAccount() => _abstractedStorageAccount.Value;

        private static Lazy<CloudStorageAccount> GetLazyStorageAccount(string connectionString)
        {
            return new Lazy<CloudStorageAccount>(() => CloudStorageAccount.Parse(connectionString));
        }

        private static Lazy<ICloudStorageAccount> GetLazyAbstractedStorageAccount(Lazy<CloudStorageAccount> lazy)
        {
            return new Lazy<ICloudStorageAccount>(() => new CloudStorageAccountWrapper(lazy.Value));
        }
    }
}

using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Worker
{
    public class CustomStorageAccountProvider : StorageAccountProvider
    {
        public const string ConnectionName = nameof(CustomStorageAccountProvider) + ":StorageAccount";

        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly ILogger<CustomStorageAccountProvider> _logger;

        private readonly object _storageAccountLock = new object();
        private StorageAccount _storageAccount;

        public CustomStorageAccountProvider(
            IConfiguration configuration,
            ServiceClientFactory serviceClientFactory,
            ILogger<CustomStorageAccountProvider> logger) : base(configuration)
        {
            _serviceClientFactory = serviceClientFactory;
            _logger = logger;
        }

        public override StorageAccount Get(string name)
        {
            switch (name)
            {
                case ConnectionName:
                    return GetStorageAccount();
                default:
                    return base.Get(name);
            }
        }

        private StorageAccount GetStorageAccount()
        {
            lock (_storageAccountLock)
            {
                if (_storageAccount != null)
                {
                    return _storageAccount;
                }

                var connectionString = _serviceClientFactory.GetStorageConnectionStringSync();
                _storageAccount = StorageAccount.NewFromConnectionString(connectionString);
                return _storageAccount;
            }
        }
    }
}

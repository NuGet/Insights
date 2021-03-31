using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage;

namespace Knapcode.ExplorePackages.Worker
{
    public class CustomStorageAccountProvider : StorageAccountProvider
    {
        public const string ConnectionName = nameof(CustomStorageAccountProvider) + ":StorageAccount";

        private readonly CloudStorageAccount _account;

        public CustomStorageAccountProvider(
            IConfiguration configuration,
            ServiceClientFactory serviceClientFactory) : base(configuration)
        {
            _account = CloudStorageAccount.Parse(serviceClientFactory.GetStorageConnectionString());
        }

        public override StorageAccount Get(string name)
        {
            switch (name)
            {
                case ConnectionName:
                    return StorageAccount.New(_account);
                default:
                    return base.Get(name);
            }
        }
    }
}

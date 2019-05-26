using Knapcode.ExplorePackages.Logic;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Worker
{
    public class ExplorePackagesStorageAccountProvider : StorageAccountProvider
    {
        private readonly IOptionsSnapshot<ExplorePackagesSettings> _options;

        public ExplorePackagesStorageAccountProvider(
            IOptionsSnapshot<ExplorePackagesSettings> options,
            IConfiguration configuration) : base(configuration)
        {
            _options = options;
        }

        public override StorageAccount Get(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return StorageAccount.NewFromConnectionString(_options.Value.StorageConnectionString);
            }

            return base.Get(name);
        }
    }
}

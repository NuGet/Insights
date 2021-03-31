using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Worker
{
    public class CustomNameResolver : DefaultNameResolver
    {
        private const string WorkerQueueKey = nameof(CustomNameResolver) + ":WorkerQueueName";
        public const string WorkerQueueVariable = "%" + WorkerQueueKey + "%";

        private readonly IOptions<ExplorePackagesWorkerSettings> _options;

        public CustomNameResolver(
            IConfiguration configuration,
            IOptions<ExplorePackagesWorkerSettings> options) : base(configuration)
        {
            _options = options;
        }

        public override string Resolve(string name)
        {
            switch (name)
            {
                case WorkerQueueKey:
                    return _options.Value.WorkerQueueName;
                default:
                    return base.Resolve(name);
            }
        }
    }
}

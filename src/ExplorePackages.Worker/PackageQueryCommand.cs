using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using Knapcode.ExplorePackages.Logic.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Worker
{
    public class PackageQueryCommand
    {
        private readonly EntityContextFactory _entityContextFactory;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly ILogger<PackageQueryCommand> _logger;

        public PackageQueryCommand(
            EntityContextFactory entityContextFactory,
            IMessageEnqueuer messageEnqueuer,
            ILogger<PackageQueryCommand> logger)
        {
            _entityContextFactory = entityContextFactory;
            _messageEnqueuer = messageEnqueuer;
            _logger = logger;
        }

        public async Task ExecuteAsync()
        {
            using (var entityContext = await _entityContextFactory.GetAsync())
            {
                var firstPackage = await entityContext
                    .Packages
                    .OrderBy(x => x.PackageKey)
                    .FirstOrDefaultAsync();
                if (firstPackage == null)
                {
                    _logger.LogInformation("There are no packages to process.");
                    return;
                }

                var lastPackage = await entityContext
                    .Packages
                    .OrderByDescending(x => x.PackageKey)
                    .FirstAsync();

                _logger.LogInformation(
                    "Found package keys {Min} through {Max}. Enqueuing a fan out message.",
                    firstPackage.PackageKey,
                    lastPackage.PackageKey);

                var message = new PackageQueryMessage(firstPackage.PackageKey, lastPackage.PackageKey);
                await _messageEnqueuer.EnqueueAsync(message);
            }
        }
    }
}

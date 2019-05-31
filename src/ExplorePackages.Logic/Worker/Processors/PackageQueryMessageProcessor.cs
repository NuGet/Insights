using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class PackageQueryMessageProcessor : IMessageProcessor<PackageQueryMessage>
    {
        private const long MaxBatchSize = 2000;

        private readonly EntityContextFactory _entityContextFactory;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly IPerPackageQueryProcessor _perPackageQueryProcessor;
        private readonly ILogger<PackageQueryMessageProcessor> _logger;

        public PackageQueryMessageProcessor(
            EntityContextFactory entityContextFactory,
            IMessageEnqueuer messageEnqueuer,
            IPerPackageQueryProcessor perPackageQueryProcessor,
            ILogger<PackageQueryMessageProcessor> logger)
        {
            _entityContextFactory = entityContextFactory;
            _messageEnqueuer = messageEnqueuer;
            _perPackageQueryProcessor = perPackageQueryProcessor;
            _logger = logger;
        }

        public async Task ProcessAsync(PackageQueryMessage message)
        {
            _logger.LogInformation(
                "Processing package query message: [{Min}, {Max}]",
                message.MinKey,
                message.MaxKey);

            var estimatedCount = (message.MaxKey - message.MinKey) + 1;
            if (estimatedCount > MaxBatchSize)
            {
                var half = estimatedCount / 2;
                var lowerMin = message.MinKey;
                var lowerMax = (message.MinKey + half) - 1;
                var upperMin = lowerMax + 1;
                var upperMax = message.MaxKey;

                _logger.LogInformation(
                    "The package query message's key range is too large. Splitting in half: " +
                    "[{LowerMin}, {LowerMax}] and [{UpperMin}, {UpperMax}]",
                    lowerMin,
                    lowerMax,
                    upperMin,
                    upperMax);

                await _messageEnqueuer.EnqueueAsync(new[]
                {
                    new PackageQueryMessage(lowerMin, lowerMax),
                    new PackageQueryMessage(upperMin, upperMax),
                });

                return;
            }

            using (var entityContext = await _entityContextFactory.GetAsync())
            {
                var packages = await entityContext
                    .Packages
                    .Where(x => message.MinKey <= x.PackageKey && x.PackageKey <= message.MaxKey)
                    .ToListAsync();

                _logger.LogInformation(
                    "Found {Count} packages in range: [{Min}, {Max}]",
                    packages.Count,
                    message.MinKey,
                    message.MaxKey);

                foreach (var package in packages)
                {
                    try
                    {
                        await _perPackageQueryProcessor.ProcessAsync(package);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Failed to process package {Id} {Version}. Enqueing a specific message for this package.",
                            package.Id,
                            package.Version);

                        var specificMessage = new PackageQueryMessage(package.PackageKey, package.PackageKey);
                        await _messageEnqueuer.EnqueueAsync(specificMessage);
                    }
                }
            }
        }
    }
}

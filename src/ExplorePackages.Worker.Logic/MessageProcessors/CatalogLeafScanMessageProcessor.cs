using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogLeafScanMessageProcessor : IMessageProcessor<CatalogLeafScanMessage>
    {
        private readonly CatalogScanDriverFactory _driverFactory;
        private readonly CatalogScanStorageService _storageService;
        private readonly MessageEnqueuer _messageEnqueuer;
        private readonly ILogger<CatalogLeafScanMessageProcessor> _logger;

        public CatalogLeafScanMessageProcessor(
            CatalogScanDriverFactory driverFactory,
            CatalogScanStorageService storageService,
            MessageEnqueuer messageEnqueuer,
            ILogger<CatalogLeafScanMessageProcessor> logger)
        {
            _driverFactory = driverFactory;
            _storageService = storageService;
            _messageEnqueuer = messageEnqueuer;
            _logger = logger;
        }

        public async Task ProcessAsync(CatalogLeafScanMessage message, int dequeueCount)
        {
            var scan = await _storageService.GetLeafScanAsync(message.StorageSuffix, message.ScanId, message.PageId, message.LeafId);
            if (scan == null)
            {
                _logger.LogWarning("No matching leaf scan was found.");
                return;
            }

            // Perform some exponential back-off for leaf-level work. This is important for cases where the leaf
            // processing performs a very taxing operation and a given worker instance may not be able to handle too
            // many leaves at once.
            var attemptCount = Math.Max(scan.AttemptCount, dequeueCount);
            if (attemptCount > 1)
            {
                var sinceLatestAttempt = DateTimeOffset.UtcNow - scan.LatestAttempt.GetValueOrDefault(scan.Created);
                var totalDelay = GetMessageDelay(attemptCount);
                var remainingDelay = totalDelay - sinceLatestAttempt;
                if (remainingDelay > TimeSpan.Zero)
                {
                    _logger.LogWarning(
                        "Catalog leaf scan has attempt count {AttemptCount} and the message has dequeue count {DequeueCount}. Waiting for {RemainingMs}ms.",
                        scan.AttemptCount,
                        dequeueCount,
                        remainingDelay.TotalMilliseconds);

                    var notBefore = TimeSpan.FromSeconds(60);
                    if (remainingDelay < notBefore)
                    {
                        notBefore = remainingDelay;
                    }

                    await _messageEnqueuer.EnqueueAsync(new[] { message }, notBefore);
                    return;
                }
            }

            scan.LatestAttempt = DateTimeOffset.UtcNow;
            scan.AttemptCount++;
            await _storageService.ReplaceAsync(scan);

            var driver = _driverFactory.Create(scan.ParsedScanType);
            await driver.ProcessLeafAsync(scan);
            await _storageService.DeleteAsync(scan);
        }

        public static TimeSpan GetMessageDelay(int attemptCount)
        {
            const int incrementMinutes = 10;
            var minMinutes = attemptCount <= 1 ? 1 : incrementMinutes * (attemptCount - 1);
            var maxMinutes = attemptCount <= 1 ? incrementMinutes : minMinutes + incrementMinutes;

            const int ms = 1000;
            var minMs = minMinutes * ms;
            var maxMs = maxMinutes * ms;

            return TimeSpan.FromMilliseconds(ThreadLocalRandom.Next(minMs, maxMs));
        }
    }
}

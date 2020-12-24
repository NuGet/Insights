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
            // processing performs a very taxing operation and a given node may not be able to handle too many leaves
            // at once.
            if (dequeueCount > 1 && message.AttemptCount < 5)
            {
                message.AttemptCount++;
                var delay = GetMessageDelay(message.AttemptCount);
                _logger.LogWarning(
                    "Catalog leaf scan message has attempt count {AttemptCount} and dequeue count {DequeueCount}. Delaying for {DelayMs}.",
                    message.AttemptCount,
                    dequeueCount,
                    delay.TotalMilliseconds);

                // This will reset the dequeue count back to 0. The next attempt will have a dequeue count of 1.
                await _messageEnqueuer.EnqueueAsync(new[] { message }, delay);
            }
            else
            {
                var driver = _driverFactory.Create(scan.ParsedScanType);

                await driver.ProcessLeafAsync(scan);

                await _storageService.DeleteAsync(scan);
            }
        }

        public static TimeSpan GetMessageDelay(int attemptCount)
        {
            const int incrementMinutes = 10;
            var minMinutes = attemptCount <= 1 ? 1 : incrementMinutes * (attemptCount - 1);
            var maxMinutes = attemptCount <= 1 ? incrementMinutes : minMinutes + incrementMinutes;
            const int ms = 1000;
            var minMs = minMinutes * ms;
            var maxMs = maxMinutes * ms;

            return TimeSpan.FromMinutes(ThreadLocalRandom.Next(minMs, maxMs));
        }
    }
}

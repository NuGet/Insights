using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogLeafScanMessageProcessor : IMessageProcessor<CatalogLeafScanMessage>
    {
        private const int MaxAttempts = 10;

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
            
            // Since we copy this message, the dequeue count will be reset. Therefore we use both the attempt count on
            // the scan record and the dequeue count on the current message copy to determine the number of attempts.
            var totalAttempts = Math.Max(scan.AttemptCount, dequeueCount);
            if (totalAttempts > MaxAttempts)
            {
                _logger.LogError("Moving message with {AttemptCount} attempts and {DequeueCount} dequeues (on the current message copy) to the poison queue.", scan.AttemptCount, dequeueCount);
                await _messageEnqueuer.EnqueuePoisonAsync(new[] { message });
                return;
            }

            // Perform some exponential back-off for leaf-level work. This is important for cases where the leaf
            // processing performs a very taxing operation and a given worker instance may not be able to handle too
            // many leaves at once.
            if (totalAttempts > 1)
            {
                var sinceLatestAttempt = DateTimeOffset.UtcNow - scan.LatestAttempt.GetValueOrDefault(scan.Created);
                var totalDelay = GetMessageDelay(totalAttempts);
                var remainingDelay = totalDelay - sinceLatestAttempt;
                if (remainingDelay > TimeSpan.Zero)
                {
                    _logger.LogWarning(
                        "Catalog leaf scan has {AttemptCount} attempts and the message has {DequeueCount} dequeues. Waiting for {RemainingMinutes:F2} minutes.",
                        scan.AttemptCount,
                        dequeueCount,
                        remainingDelay.TotalMinutes);

                    var notBefore = TimeSpan.FromSeconds(60);
                    if (remainingDelay < notBefore)
                    {
                        notBefore = remainingDelay;
                    }

                    await _messageEnqueuer.EnqueueAsync(new[] { message }, notBefore);
                    return;
                }
            }

            _logger.LogInformation("Attempting catalog leaf scan.");
            scan.LatestAttempt = DateTimeOffset.UtcNow;
            scan.AttemptCount++;
            await _storageService.ReplaceAsync(scan);

            var driver = _driverFactory.Create(scan.ParsedScanType);
            await driver.ProcessLeafAsync(scan);

            _logger.LogInformation("Completed catalog leaf scan.");
            await _storageService.DeleteAsync(scan);
        }

        public static TimeSpan GetMessageDelay(int attemptCount)
        {
            const int incrementMinutes = 5;
            var minMinutes = attemptCount <= 1 ? 1 : incrementMinutes * (attemptCount - 1);
            var maxMinutes = attemptCount <= 1 ? incrementMinutes : minMinutes + incrementMinutes;

            const int msPerMinute = 60 * 1000;
            var minMs = minMinutes * msPerMinute;
            var maxMs = maxMinutes * msPerMinute;

            return TimeSpan.FromMilliseconds(ThreadLocalRandom.Next(minMs, maxMs));
        }
    }
}

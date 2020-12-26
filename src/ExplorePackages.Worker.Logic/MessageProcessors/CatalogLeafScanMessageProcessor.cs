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
            // the scan record (plus 1, for the current attempt) and the dequeue count on the current message copy to
            // determine the number of attempts. We want to use the dequeue count because it will always go up, even if
            // we fail to update the attempt count on the record.
            var totalAttempts = Math.Max(scan.AttemptCount + 1, dequeueCount);
            if (totalAttempts > MaxAttempts)
            {
                _logger.LogError("Moving message with {AttemptCount} attempts and {DequeueCount} dequeues (on the current message copy) to the poison queue.", scan.AttemptCount, dequeueCount);
                await _messageEnqueuer.EnqueuePoisonAsync(new[] { message });
                return;
            }

            // Perform some exponential back-off for leaf-level work. This is important for cases where the leaf
            // processing performs a very taxing operation and a given worker instance may not be able to handle too
            // many leaves at once.
            if (scan.NextAttempt.HasValue)
            {
                var untilNextAttempt = DateTimeOffset.UtcNow - scan.NextAttempt.Value;
                if (untilNextAttempt > TimeSpan.Zero)
                {
                    _logger.LogWarning(
                        "Catalog leaf scan has {AttemptCount} attempts and the message has {DequeueCount} dequeues. Waiting for {RemainingMinutes:F2} minutes.",
                        scan.AttemptCount,
                        dequeueCount,
                        untilNextAttempt.TotalMinutes);

                    var notBefore = TimeSpan.FromSeconds(60);
                    if (untilNextAttempt < notBefore)
                    {
                        notBefore = untilNextAttempt;
                    }

                    await _messageEnqueuer.EnqueueAsync(new[] { message }, notBefore);
                    return;
                }
            }

            scan.AttemptCount++;
            scan.NextAttempt = DateTime.UtcNow + GetMessageDelay(totalAttempts);
            await _storageService.ReplaceAsync(scan);

            _logger.LogInformation("Attempting catalog leaf scan.");
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

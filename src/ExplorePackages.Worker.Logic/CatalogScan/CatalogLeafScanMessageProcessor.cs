using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogLeafScanMessageProcessor : IBatchMessageProcessor<CatalogLeafScanMessage>
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

        public async Task<BatchMessageProcessorResult<CatalogLeafScanMessage>> ProcessAsync(IReadOnlyList<CatalogLeafScanMessage> messages, int dequeueCount)
        {
            var tryAgainLater = new List<(CatalogLeafScanMessage Message, CatalogLeafScan Scan, TimeSpan NotBefore)>();
            var noMatchingLeaf = new List<CatalogLeafScanMessage>();
            var poison = new List<(CatalogLeafScanMessage Message, CatalogLeafScan Scan)>();

            foreach (var pageGroup in messages.GroupBy(x => (x.StorageSuffix, x.ScanId, x.PageId)))
            {
                var driverTypeToProcess = new Dictionary<CatalogScanDriverType, List<(CatalogLeafScanMessage Message, CatalogLeafScan Scan)>>();

                await CategorizeMessagesAsync(pageGroup, dequeueCount, noMatchingLeaf, poison, tryAgainLater, driverTypeToProcess);

                if (driverTypeToProcess.Count > 1)
                {
                    throw new InvalidOperationException("For a single scan ID, there should be no more than one driver type.");
                }

                if (noMatchingLeaf.Any())
                {
                    _logger.LogWarning("There were {Count} messages with no matching leaf scans.", noMatchingLeaf.Count);
                }

                foreach (var (driverType, toProcess) in driverTypeToProcess)
                {
                    var batchDriver = _driverFactory.CreateBatchDriverOrNull(driverType);
                    if (batchDriver != null)
                    {
                        await ProcessBatchAsync(batchDriver, dequeueCount, tryAgainLater, toProcess);
                    }
                    else
                    {
                        await ProcessBatchOneByOneAsync(dequeueCount, tryAgainLater, toProcess);
                    }
                }
            }

            if (poison.Any())
            {
                // Enqueue these one at a time to ease debugging.
                foreach (var (message, scan) in poison)
                {
                    _logger.LogError("Moving message with {AttemptCount} attempts and {DequeueCount} dequeues (on the current message copy) to the poison queue.", scan.AttemptCount, dequeueCount);
                    await _messageEnqueuer.EnqueuePoisonAsync(poison);
                }
            }

            if (tryAgainLater.Any())
            {
                foreach (var (message, scan, notBefore) in tryAgainLater)
                {
                    _logger.LogWarning(
                        "Catalog leaf scan has {AttemptCount} attempts and the message has {DequeueCount} dequeues. Waiting for {RemainingMinutes:F2} minutes.",
                        scan.AttemptCount,
                        dequeueCount,
                        notBefore.TotalMinutes);
                }
            }

            return new BatchMessageProcessorResult<CatalogLeafScanMessage>(tryAgainLater.Select(x => (x.Message, x.NotBefore)));
        }

        private async Task CategorizeMessagesAsync(
            IGrouping<(string StorageSuffix, string ScanId, string PageId), CatalogLeafScanMessage> pageGroup,
            int dequeueCount,
            List<CatalogLeafScanMessage> noMatchingLeaf,
            List<(CatalogLeafScanMessage Message, CatalogLeafScan Scan)> poison,
            List<(CatalogLeafScanMessage Message, CatalogLeafScan Scan, TimeSpan NotBefore)> tryAgainLater,
            Dictionary<CatalogScanDriverType, List<(CatalogLeafScanMessage Message, CatalogLeafScan Scan)>> driverTypeToProcess)
        {
            var leafIdToMessage = pageGroup.ToDictionary(x => x.LeafId);
            var leafIdToScan = await _storageService.GetLeafScansAsync(
                pageGroup.Key.StorageSuffix,
                pageGroup.Key.ScanId,
                pageGroup.Key.PageId,
                leafIdToMessage.Keys);

            foreach (var message in pageGroup)
            {
                if (!leafIdToScan.TryGetValue(message.LeafId, out var scan))
                {
                    noMatchingLeaf.Add(message);
                }

                // Since we copy this message, the dequeue count will be reset. Therefore we use both the attempt count on
                // the scan record (plus 1, for the current attempt) and the dequeue count on the current message copy to
                // determine the number of attempts. We want to use the dequeue count because it will always go up, even if
                // we fail to update the attempt count on the record.
                if (GetTotalAttempts(scan, dequeueCount) > MaxAttempts)
                {
                    poison.Add((message, scan));
                }

                // Perform some exponential back-off for leaf-level work. This is important for cases where the leaf
                // processing performs a very taxing operation and a given worker instance may not be able to handle too
                // many leaves at once.
                if (scan.NextAttempt.HasValue)
                {
                    var untilNextAttempt = scan.NextAttempt.Value - DateTimeOffset.UtcNow;
                    if (untilNextAttempt > TimeSpan.Zero)
                    {
                        var notBefore = TimeSpan.FromMinutes(10);
                        if (untilNextAttempt < notBefore)
                        {
                            notBefore = untilNextAttempt;
                        }

                        tryAgainLater.Add((message, scan, notBefore));
                    }
                }

                if (!driverTypeToProcess.TryGetValue(scan.ParsedDriverType, out var toProcess))
                {
                    toProcess = new List<(CatalogLeafScanMessage Message, CatalogLeafScan Scan)>();
                    driverTypeToProcess.Add(scan.ParsedDriverType, toProcess);
                }

                toProcess.Add((message, scan));
            }
        }

        private async Task ProcessBatchAsync(
            ICatalogLeafScanBatchDriver batchDriver,
            int dequeueCount,
            List<(CatalogLeafScanMessage Message, CatalogLeafScan Scan, TimeSpan NotBefore)> tryAgainLater,
            List<(CatalogLeafScanMessage Message, CatalogLeafScan Scan)> toProcess)
        {
            _logger.LogInformation("Attempting batch of {Count} catalog leaf scans.", toProcess.Count);

            // Increment the attempt counter for all scans
            var scans = new List<CatalogLeafScan>();
            var scanToMessage = new Dictionary<CatalogLeafScan, CatalogLeafScanMessage>(ReferenceEqualityComparer<CatalogLeafScan>.Instance);
            foreach (var (message, scan) in toProcess)
            {
                scans.Add(scan);
                scanToMessage.Add(scan, message);
                StartAttempt(scan, dequeueCount);
            }
            await _storageService.ReplaceAsync(scans);

            // Execute the batch logic
            var result = await batchDriver.ProcessLeavesAsync(scans);

            // Delete all successful scans
            await _storageService.DeleteAsync(scanToMessage.Keys);

            // Reduce the attempt counter for all "try again later" scans
            var allTryAgainLaterScans = new List<CatalogLeafScan>();
            foreach (var (notBefore, tryAgainLaterScans) in result.TryAgainLater)
            {
                foreach (var scan in tryAgainLaterScans)
                {
                    allTryAgainLaterScans.Add(scan);
                    tryAgainLater.Add((scanToMessage[scan], scan, notBefore));
                    scanToMessage.Remove(scan);
                    ResetAttempt(scan);
                }
            }
            await _storageService.ReplaceAsync(allTryAgainLaterScans);
        }

        private async Task ProcessBatchOneByOneAsync(
            int dequeueCount,
            List<(CatalogLeafScanMessage Message, CatalogLeafScan Scan, TimeSpan NotBefore)> tryAgainLater,
            List<(CatalogLeafScanMessage Message, CatalogLeafScan Scan)> toProcess)
        {
            foreach (var (message, scan) in toProcess)
            {
                // Rebuild the driver for each message, to minimize interactions.
                var nonBatchDriver = _driverFactory.CreateNonBatchDriver(scan.ParsedDriverType);

                _logger.LogInformation("Attempting catalog leaf scan.");

                // Increment the attempt counter for the scan
                StartAttempt(scan, dequeueCount);
                await _storageService.ReplaceAsync(scan);

                // Execute the non-batch logic
                var result = await nonBatchDriver.ProcessLeafAsync(scan);

                switch (result.Type)
                {
                    case DriverResultType.Success:
                        _logger.LogInformation("Completed catalog leaf scan.");
                        await _storageService.DeleteAsync(scan);
                        break;
                    case DriverResultType.TryAgainLater:
                        _logger.LogInformation("Catalog leaf scan will be tried again later.");
                        ResetAttempt(scan);
                        await _storageService.ReplaceAsync(scan);
                        tryAgainLater.Add((message, scan, TimeSpan.FromMinutes(1)));
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }
        }

        private static void StartAttempt(CatalogLeafScan scan, int dequeueCount)
        {
            scan.AttemptCount++;
            scan.NextAttempt = DateTime.UtcNow + GetMessageDelay(GetTotalAttempts(scan, dequeueCount));
        }

        private static void ResetAttempt(CatalogLeafScan scan)
        {
            scan.AttemptCount--;
        }

        private static int GetTotalAttempts(CatalogLeafScan scan, int dequeueCount)
        {
            return Math.Max(scan.AttemptCount + 1, dequeueCount);
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

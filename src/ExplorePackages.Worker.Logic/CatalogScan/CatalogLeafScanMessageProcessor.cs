using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogLeafScanMessageProcessor : IBatchMessageProcessor<CatalogLeafScanMessage>
    {
        private const int MaxAttempts = 10;

        private readonly ICatalogScanDriverFactory _driverFactory;
        private readonly CatalogScanStorageService _storageService;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly ILogger<CatalogLeafScanMessageProcessor> _logger;

        public CatalogLeafScanMessageProcessor(
            ICatalogScanDriverFactory driverFactory,
            CatalogScanStorageService storageService,
            IMessageEnqueuer messageEnqueuer,
            ILogger<CatalogLeafScanMessageProcessor> logger)
        {
            _driverFactory = driverFactory;
            _storageService = storageService;
            _messageEnqueuer = messageEnqueuer;
            _logger = logger;
        }

        public async Task<BatchMessageProcessorResult<CatalogLeafScanMessage>> ProcessAsync(IReadOnlyList<CatalogLeafScanMessage> messages, long dequeueCount)
        {
            var throwOnException = messages.Count == 1;
            var failed = new List<CatalogLeafScanMessage>();
            var tryAgainLater = new List<(CatalogLeafScanMessage Message, CatalogLeafScan Scan, TimeSpan NotBefore)>();
            var noMatchingScan = new List<CatalogLeafScanMessage>();
            var poison = new List<(CatalogLeafScanMessage Message, CatalogLeafScan Scan)>();

            foreach (var pageGroup in messages.GroupBy(x => (x.StorageSuffix, x.ScanId, x.PageId)))
            {
                var driverTypeToProcess = new Dictionary<CatalogScanDriverType, List<(CatalogLeafScanMessage Message, CatalogLeafScan Scan)>>();

                await CategorizeMessagesAsync(pageGroup, dequeueCount, noMatchingScan, poison, tryAgainLater, driverTypeToProcess);

                if (driverTypeToProcess.Count > 1)
                {
                    throw new InvalidOperationException("For a single scan ID, there should be no more than one driver type.");
                }

                foreach ((var driverType, var toProcess) in driverTypeToProcess)
                {
                    var batchDriver = _driverFactory.CreateBatchDriverOrNull(driverType);
                    if (batchDriver != null)
                    {
                        await ProcessBatchAsync(batchDriver, dequeueCount, failed, tryAgainLater, toProcess, throwOnException);
                    }
                    else
                    {
                        await ProcessOneByOneAsync(dequeueCount, failed, tryAgainLater, toProcess, throwOnException);
                    }
                }
            }

            if (noMatchingScan.Any())
            {
                _logger.LogWarning("There were {NoMatchingScanCount} messages of {Count} with no matching leaf scans.", noMatchingScan.Count, messages.Count);
            }

            if (failed.Any())
            {
                _logger.LogError("{FailedCount} catalog leaf scans of {Count} failed.", failed.Count, messages.Count);
            }

            if (tryAgainLater.Any())
            {
                _logger.LogWarning(
                    "{TryAgainLaterCount} catalog leaf scans of {Count} will be tried again later, in {Min} to {Max}.",
                    tryAgainLater.Count,
                    messages.Count,
                    tryAgainLater.Min(x => x.NotBefore),
                    tryAgainLater.Max(x => x.NotBefore));
            }

            if (poison.Any())
            {
                _logger.LogError("{PoisonCount} catalog leaf scans of {Count} will be moved to the poison queue.", poison.Count, messages.Count);

                // Enqueue these one at a time to ease debugging.
                foreach ((var message, var scan) in poison)
                {
                    _logger.LogError("Moving message with {AttemptCount} attempts and {DequeueCount} dequeues to the poison queue.", scan.AttemptCount, dequeueCount);
                    await _messageEnqueuer.EnqueuePoisonAsync(new[] { message });
                }
            }

            return new BatchMessageProcessorResult<CatalogLeafScanMessage>(
                failed,
                tryAgainLater.Select(x => (x.Message, x.NotBefore)));
        }

        private async Task CategorizeMessagesAsync(
            IGrouping<(string StorageSuffix, string ScanId, string PageId), CatalogLeafScanMessage> pageGroup,
            long dequeueCount,
            List<CatalogLeafScanMessage> noMatchingScan,
            List<(CatalogLeafScanMessage Message, CatalogLeafScan Scan)> poison,
            List<(CatalogLeafScanMessage Message, CatalogLeafScan Scan, TimeSpan NotBefore)> tryAgainLater,
            Dictionary<CatalogScanDriverType, List<(CatalogLeafScanMessage Message, CatalogLeafScan Scan)>> driverTypeToProcess)
        {
            var leafIdToMessage = pageGroup.ToDictionary(x => x.LeafId);

            IReadOnlyDictionary<string, CatalogLeafScan> leafIdToScan;
            try
            {
                leafIdToScan = await _storageService.GetLeafScansAsync(
                    pageGroup.Key.StorageSuffix,
                    pageGroup.Key.ScanId,
                    pageGroup.Key.PageId,
                    leafIdToMessage.Keys);
            }
            catch (StorageException ex) when (ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.NotFound)
            {
                // Handle the missing table case.
                noMatchingScan.AddRange(pageGroup);
                return;
            }

            foreach (var message in pageGroup)
            {
                if (!leafIdToScan.TryGetValue(message.LeafId, out var scan))
                {
                    noMatchingScan.Add(message);
                    continue;
                }

                // Since we copy this message, the dequeue count will be reset. Therefore we use both the attempt count on
                // the scan record (plus 1, for the current attempt) and the dequeue count on the current message copy to
                // determine the number of attempts. We want to use the dequeue count because it will always go up, even if
                // we fail to update the attempt count on the record.
                if (GetTotalAttempts(scan, dequeueCount) > MaxAttempts)
                {
                    poison.Add((message, scan));
                    continue;
                }

                // Perform some exponential back-off for leaf-level work. This is important for cases where the leaf
                // processing performs a very taxing operation and a given worker instance may not be able to handle too
                // many leaves at once.
                if (scan.NextAttempt.HasValue)
                {
                    var untilNextAttempt = scan.NextAttempt.Value - DateTimeOffset.UtcNow;
                    if (untilNextAttempt > TimeSpan.Zero)
                    {
                        // To account for clock skew, wait an extra 5 seconds.
                        untilNextAttempt += TimeSpan.FromSeconds(5);

                        var notBefore = TimeSpan.FromMinutes(5);
                        if (untilNextAttempt < notBefore)
                        {
                            notBefore = untilNextAttempt;
                        }

                        tryAgainLater.Add((message, scan, notBefore));
                        continue;
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
            long dequeueCount,
            List<CatalogLeafScanMessage> failed,
            List<(CatalogLeafScanMessage Message, CatalogLeafScan Scan, TimeSpan NotBefore)> tryAgainLater,
            List<(CatalogLeafScanMessage Message, CatalogLeafScan Scan)> toProcess,
            bool throwOnException)
        {
            _logger.LogInformation("Attempting batch of {Count} catalog leaf scans.", toProcess.Count);

            // Increment the attempt counter for all scans
            var scans = new List<CatalogLeafScan>();
            var scanToMessage = new Dictionary<CatalogLeafScan, CatalogLeafScanMessage>(ReferenceEqualityComparer<CatalogLeafScan>.Instance);
            foreach ((var message, var scan) in toProcess)
            {
                scans.Add(scan);
                scanToMessage.Add(scan, message);
                StartAttempt(scan, dequeueCount);
            }
            await _storageService.ReplaceAsync(scans);

            // Execute the batch logic
            BatchMessageProcessorResult<CatalogLeafScan> result;
            try
            {
                result = await batchDriver.ProcessLeavesAsync(scans);
            }
            catch (Exception ex) when (!throwOnException)
            {
                _logger.LogError(ex, "Processing a catalog leaf scan batch failed.");
                failed.AddRange(scans.Select(x => scanToMessage[x]));
                return;
            }

            // Reduce the attempt counter for all "try again later" scans and remove them from the set to delete (complete)
            var allTryAgainLaterScans = new List<CatalogLeafScan>();
            foreach ((var notBefore, var tryAgainLaterScans) in result.TryAgainLater)
            {
                foreach (var scan in tryAgainLaterScans)
                {
                    allTryAgainLaterScans.Add(scan);
                    tryAgainLater.Add((scanToMessage[scan], scan, notBefore));
                    scanToMessage.Remove(scan);
                    ResetAttempt(scan);
                }
            }

            // Remove failed scans from the set to delete (complete)
            foreach (var scan in result.Failed)
            {
                failed.Add(scanToMessage[scan]);
                scanToMessage.Remove(scan);
            }

            // Delete all successful scans
            await _storageService.DeleteAsync(scanToMessage.Keys);

            // Update the "try again later" scans
            await _storageService.ReplaceAsync(allTryAgainLaterScans);
        }

        private async Task ProcessOneByOneAsync(
            long dequeueCount,
            List<CatalogLeafScanMessage> failed,
            List<(CatalogLeafScanMessage Message, CatalogLeafScan Scan, TimeSpan NotBefore)> tryAgainLater,
            List<(CatalogLeafScanMessage Message, CatalogLeafScan Scan)> toProcess,
            bool throwOnException)
        {
            foreach ((var message, var scan) in toProcess)
            {
                // Rebuild the driver for each message, to minimize interactions.
                var nonBatchDriver = _driverFactory.CreateNonBatchDriver(scan.ParsedDriverType);

                _logger.LogInformation("Attempting catalog leaf scan.");

                // Increment the attempt counter for the scan
                StartAttempt(scan, dequeueCount);
                await _storageService.ReplaceAsync(scan);

                // Execute the non-batch logic
                try
                {
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
                catch (Exception ex) when (!throwOnException)
                {
                    _logger.LogError(ex, "Processing a catalog leaf scan failed.");
                    failed.Add(message);
                }
            }
        }

        private static void StartAttempt(CatalogLeafScan scan, long dequeueCount)
        {
            scan.AttemptCount++;
            scan.NextAttempt = DateTime.UtcNow + GetMessageDelay(GetTotalAttempts(scan, dequeueCount));
        }

        private static void ResetAttempt(CatalogLeafScan scan)
        {
            scan.AttemptCount--;
            scan.NextAttempt = DateTime.UtcNow;
        }

        private static long GetTotalAttempts(CatalogLeafScan scan, long dequeueCount)
        {
            return Math.Max(scan.AttemptCount + 1, dequeueCount);
        }

        public static TimeSpan GetMessageDelay(long attemptCount)
        {
            // First attempt, wait up to a minute.
            const int msPerMinute = 60 * 1000;
            if (attemptCount <= 1)
            {
                return TimeSpan.FromMilliseconds(ThreadLocalRandom.Next(0, msPerMinute));
            }

            // Then try in increments of more minutes.
            const int incrementMinutes = 2;
            var minMinutes = incrementMinutes * (attemptCount - 1);
            var maxMinutes = minMinutes + incrementMinutes;

            var minMs = minMinutes * msPerMinute;
            var maxMs = maxMinutes * msPerMinute;

            return TimeSpan.FromMilliseconds(ThreadLocalRandom.Next((int)minMs, (int)maxMs));
        }
    }
}

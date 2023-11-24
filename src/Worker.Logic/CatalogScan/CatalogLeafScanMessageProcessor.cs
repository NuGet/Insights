// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Azure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NuGet.Insights.Worker
{
    public class CatalogLeafScanMessageProcessor : IBatchMessageProcessor<CatalogLeafScanMessage>
    {
        private const int MaxAttempts = 10;
        private static readonly TimeSpan TryAgainLaterDuration = TimeSpan.FromMinutes(1);

        private readonly ICatalogScanDriverFactory _driverFactory;
        private readonly CatalogScanStorageService _storageService;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly ITelemetryClient _telemetryClient;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ILogger<CatalogLeafScanMessageProcessor> _logger;

        public CatalogLeafScanMessageProcessor(
            ICatalogScanDriverFactory driverFactory,
            CatalogScanStorageService storageService,
            IMessageEnqueuer messageEnqueuer,
            ITelemetryClient telemetryClient,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger<CatalogLeafScanMessageProcessor> logger)
        {
            _driverFactory = driverFactory;
            _storageService = storageService;
            _messageEnqueuer = messageEnqueuer;
            _telemetryClient = telemetryClient;
            _options = options;
            _logger = logger;
        }

        public async Task<BatchMessageProcessorResult<CatalogLeafScanMessage>> ProcessAsync(IReadOnlyList<CatalogLeafScanMessage> messages, long dequeueCount)
        {
            var throwOnException = messages.Count == 1;
            var failed = new List<CatalogLeafScanMessage>();
            var tryAgainLater = new List<(CatalogLeafScanMessage Message, CatalogLeafScan Scan, TimeSpan NotBefore)>();
            var noMatchingScan = new List<CatalogLeafScanMessage>();
            var poison = new List<(CatalogLeafScanMessage Message, CatalogLeafScan Scan)>();
            var countMetric = _telemetryClient.GetMetric("CatalogLeafScan.Count", "DriverType", "RangeType");

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

                    EmitCountMetric(countMetric, driverType, toProcess.Select(x => x.Scan));

                    if (batchDriver != null)
                    {
                        await ProcessBatchAsync(driverType, batchDriver, dequeueCount, failed, tryAgainLater, toProcess, throwOnException);
                    }
                    else
                    {
                        await ProcessOneByOneAsync(dequeueCount, failed, tryAgainLater, toProcess, throwOnException);
                    }
                }
            }

            if (noMatchingScan.Any())
            {
                _logger.LogTransientWarning("There were {NoMatchingScanCount} messages of {Count} with no matching leaf scans.", noMatchingScan.Count, messages.Count);
            }

            if (failed.Any())
            {
                _logger.LogError("{FailedCount} catalog leaf scans of {Count} failed.", failed.Count, messages.Count);
            }

            if (tryAgainLater.Any())
            {
                _logger.LogTransientWarning(
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

        private void EmitCountMetric(IMetric metric, CatalogScanDriverType driverType, IEnumerable<CatalogLeafScan> scans)
        {
            var driverTypeString = driverType.ToString();
            var bucketRangeCount = 0;
            var catalogRangeCount = 0;

            foreach (var scan in scans)
            {
                if (scan.BucketRanges is not null)
                {
                    bucketRangeCount++;
                }
                else
                {
                    catalogRangeCount++;
                }
            }

            if (bucketRangeCount > 0)
            {
                metric.TrackValue(bucketRangeCount, driverTypeString, "Bucket");
            }

            if (catalogRangeCount > 0)
            {
                metric.TrackValue(catalogRangeCount, driverTypeString, "Commit");
            }
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
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
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

                if (!driverTypeToProcess.TryGetValue(scan.DriverType, out var toProcess))
                {
                    toProcess = new List<(CatalogLeafScanMessage Message, CatalogLeafScan Scan)>();
                    driverTypeToProcess.Add(scan.DriverType, toProcess);
                }

                toProcess.Add((message, scan));

                if (scan.Max - scan.Min <= _options.Value.LeafLevelTelemetryThreshold)
                {
                    _telemetryClient.TrackMetric($"{nameof(CatalogLeafScanMessageProcessor)}.ToProcess.{nameof(CatalogLeafScan)}", 1, new Dictionary<string, string>
                    {
                        { nameof(CatalogLeafScanMessage.StorageSuffix), scan.StorageSuffix },
                        { nameof(CatalogLeafScanMessage.ScanId), scan.ScanId },
                        { nameof(CatalogLeafScanMessage.PageId), scan.PageId },
                        { nameof(CatalogLeafScanMessage.LeafId), scan.LeafId },
                    });
                }
            }
        }

        private async Task ProcessBatchAsync(
            CatalogScanDriverType driverType,
            ICatalogLeafScanBatchDriver batchDriver,
            long dequeueCount,
            List<CatalogLeafScanMessage> failed,
            List<(CatalogLeafScanMessage Message, CatalogLeafScan Scan, TimeSpan NotBefore)> tryAgainLater,
            List<(CatalogLeafScanMessage Message, CatalogLeafScan Scan)> toProcess,
            bool throwOnException)
        {
            _logger.LogInformation("Starting batch of {Count} {DriverType} catalog leaf scans.", toProcess.Count, driverType);

            var scans = new List<CatalogLeafScan>();
            var scanToMessage = new Dictionary<CatalogLeafScan, CatalogLeafScanMessage>(ReferenceEqualityComparer<CatalogLeafScan>.Instance);
            var attempted = new HashSet<CatalogLeafScan>(ReferenceEqualityComparer<CatalogLeafScan>.Instance);
            foreach ((var message, var scan) in toProcess)
            {
                scans.Add(scan);
                scanToMessage.Add(scan, message);
            }

            List<CatalogLeafScan> TryAgainLater(IReadOnlyDictionary<TimeSpan, IReadOnlyList<CatalogLeafScan>> notBeforeToScans)
            {
                var allTryAgainLaterScans = new List<CatalogLeafScan>();
                foreach ((var notBefore, var tryAgainLaterScans) in notBeforeToScans)
                {
                    foreach (var scan in tryAgainLaterScans)
                    {
                        allTryAgainLaterScans.Add(scan);
                        tryAgainLater.Add((scanToMessage[scan], scan, notBefore));
                        scanToMessage.Remove(scan);
                        if (attempted.Remove(scan))
                        {
                            ResetAttempt(scan);
                        }
                    }
                }

                return allTryAgainLaterScans;
            }

            async Task<bool> UpdateCatalogLeafScansAsync(IReadOnlyList<CatalogLeafScan> scans, Func<IReadOnlyList<CatalogLeafScan>, Task> setStorageAsync)
            {
                if (scans.Count == 0)
                {
                    return true;
                }

                try
                {
                    await setStorageAsync(scans);
                    return true;
                }
                catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
                {
                    _logger.LogTransientWarning("Another thread already completed one of the catalog leaf scans in the batch. Trying again later.");
                    TryAgainLater(new Dictionary<TimeSpan, IReadOnlyList<CatalogLeafScan>> { { TryAgainLaterDuration, scans } });
                    return false;
                }
                catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.PreconditionFailed)
                {
                    _logger.LogTransientWarning("Another thread is already processing one of the catalog leaf scans in the batch. Trying again later.");
                    TryAgainLater(new Dictionary<TimeSpan, IReadOnlyList<CatalogLeafScan>> { { TryAgainLaterDuration, scans } });
                    return false;
                }
            }

            // Increment the attempt counter for all scans
            foreach (var scan in scans)
            {
                StartAttempt(scan, dequeueCount);
                attempted.Add(scan);
            }

            if (!await UpdateCatalogLeafScansAsync(scans, _storageService.ReplaceAsync))
            {
                return;
            }

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
            var allTryAgainLaterScans = TryAgainLater(result.TryAgainLater);

            // Remove failed scans from the set to delete (complete)
            foreach (var scan in result.Failed)
            {
                failed.Add(scanToMessage[scan]);
                scanToMessage.Remove(scan);
            }

            // Update the "try again later" scans
            await UpdateCatalogLeafScansAsync(allTryAgainLaterScans, _storageService.ReplaceAsync);

            // Delete all successful scans
            await UpdateCatalogLeafScansAsync(scanToMessage.Keys.ToList(), _storageService.DeleteAsync);
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
                var nonBatchDriver = _driverFactory.CreateNonBatchDriver(scan.DriverType);

                _logger.LogInformation("Starting {DriverType} catalog leaf scan for {Id} {Version}.", scan.DriverType, scan.PackageId, scan.PackageVersion);

                async Task<bool> UpdateCatalogLeafScanAsync(Func<CatalogLeafScan, Task> setStorageAsync)
                {
                    try
                    {
                        await setStorageAsync(scan);
                        return true;
                    }
                    catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
                    {
                        _logger.LogTransientWarning("Another thread already completed catalog leaf scan for {Id} {Version}.", scan.PackageId, scan.PackageVersion);
                        return false;
                    }
                    catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.PreconditionFailed)
                    {
                        _logger.LogTransientWarning("Another thread is already processing catalog leaf scan for {Id} {Version}. Trying again later.", scan.PackageId, scan.PackageVersion);
                        tryAgainLater.Add((message, scan, TryAgainLaterDuration));
                        return false;
                    }
                }

                // Increment the attempt counter for the scan
                StartAttempt(scan, dequeueCount);
                if (!await UpdateCatalogLeafScanAsync(_storageService.ReplaceAsync))
                {
                    continue;
                }

                // Execute the non-batch logic
                DriverResult result;
                try
                {
                    result = await nonBatchDriver.ProcessLeafAsync(scan);
                }
                catch (Exception ex) when (!throwOnException)
                {
                    _logger.LogError(ex, "Processing a catalog leaf scan failed for {Id} {Version}.", scan.PackageId, scan.PackageVersion);
                    failed.Add(message);
                    continue;
                }

                // Update storage with respect to the driver result
                switch (result.Type)
                {
                    case DriverResultType.Success:
                        _logger.LogInformation("Completed catalog leaf scan.");
                        await UpdateCatalogLeafScanAsync(_storageService.DeleteAsync);
                        break;
                    case DriverResultType.TryAgainLater:
                        _logger.LogInformation("Catalog leaf scan will be tried again later.");
                        ResetAttempt(scan);
                        if (await UpdateCatalogLeafScanAsync(_storageService.ReplaceAsync))
                        {
                            tryAgainLater.Add((message, scan, TryAgainLaterDuration));
                        }
                        break;
                    default:
                        throw new NotImplementedException();
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

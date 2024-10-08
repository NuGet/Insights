// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.LoadBucketedPackage;

namespace NuGet.Insights.Worker.TimedReprocess
{
    public class TimedReprocessMessageProcessor : IMessageProcessor<TimedReprocessMessage>
    {
        private readonly TimedReprocessService _service;
        private readonly TimedReprocessStorageService _storageService;
        private readonly CatalogScanService _catalogScanService;
        private readonly CatalogScanStorageService _catalogScanStorageService;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly ILogger<TimedReprocessMessageProcessor> _logger;

        public TimedReprocessMessageProcessor(
            TimedReprocessService service,
            TimedReprocessStorageService storageService,
            CatalogScanService catalogScanService,
            CatalogScanStorageService catalogScanStorageService,
            IMessageEnqueuer messageEnqueuer,
            ILogger<TimedReprocessMessageProcessor> logger)
        {
            _service = service;
            _storageService = storageService;
            _catalogScanService = catalogScanService;
            _catalogScanStorageService = catalogScanStorageService;
            _messageEnqueuer = messageEnqueuer;
            _logger = logger;
        }

        public async Task ProcessAsync(TimedReprocessMessage message, long dequeueCount)
        {
            var run = await _storageService.GetRunAsync(message.RunId);
            if (run is null)
            {
                if (message.AttemptCount < 10)
                {
                    _logger.LogTransientWarning("After {AttemptCount} attempts, the timed reprocess run {RunId} should have already been created. Trying again.",
                        message.AttemptCount,
                        message.RunId);
                    message.AttemptCount++;
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount));
                }
                else
                {
                    _logger.LogTransientWarning("After {AttemptCount} attempts, the timed reprocess run {RunId} should have already been created. Giving up.",
                        message.AttemptCount,
                        message.RunId);
                }

                return;
            }

            if (run.State == TimedReprocessState.Created)
            {
                run.Started = DateTimeOffset.UtcNow;
                run.State = TimedReprocessState.Working;
                await _storageService.ReplaceRunAsync(run);
            }

            var buckets = BucketRange.ParseBuckets(run.BucketRanges).ToList();
            if (buckets.Count == 0)
            {
                throw new InvalidOperationException("Buckets for the timed reprocess run should have been set by now.");
            }

            if (run.State == TimedReprocessState.Working)
            {
                var timedScans = await _storageService.GetScansAsync(run.RunId);
                var batches = _service.GetReprocessBatches();

                for (var i = 0; i < batches.Count; i++)
                {
                    var batch = batches[i];
                    var incompleteCount = 0;

                    foreach (var driverType in batch)
                    {
                        var timedScan = timedScans.SingleOrDefault(x => x.DriverType == driverType);
                        if (timedScan == null)
                        {
                            var descendingId = StorageUtility.GenerateDescendingId();
                            var scanId = CatalogScanService.GetBucketRangeScanId(BucketRange.ParseBuckets(run.BucketRanges), descendingId);
                            var storageSuffix = descendingId.Unique;
                            timedScan = new TimedReprocessCatalogScan(run.RunId, driverType, scanId, storageSuffix);

                            timedScan = await _storageService.GetOrAddScanAsync(timedScan);
                            timedScans.Add(timedScan);

                            _logger.LogInformation("Starting catalog scan for {DriverType} with scan ID {ScanId}.", driverType, timedScan.ScanId);
                        }

                        if (!timedScan.Completed)
                        {
                            // Check for the timed scan before trying to start it. This reduces confusing telemetry.
                            var existing = await _catalogScanStorageService.GetIndexScanAsync(timedScan.DriverType, timedScan.ScanId);
                            if (existing is not null && !existing.State.IsTerminal())
                            {
                                _logger.LogInformation(
                                    "Catalog scan {ScanId} for {DriverType} driver is in the {State} state.",
                                    timedScan.ScanId,
                                    driverType,
                                    existing.State);
                                incompleteCount++;
                            }
                            else
                            {
                                var result = await _catalogScanService.UpdateAsync(
                                    timedScan.ScanId,
                                    timedScan.StorageSuffix,
                                    timedScan.DriverType,
                                    buckets);

                                if (result.Scan is not null && result.Scan.ScanId != timedScan.ScanId)
                                {
                                    // It's possible a cursor-based scan is running right now. Wait for it to complete
                                    // before starting the bucket range scan starts.
                                    _logger.LogInformation("Another scan for {DriverType} is already running. The scan ID is {ScanId}.", driverType, result.Scan.ScanId);
                                    incompleteCount++;
                                }
                                else if (result.Type == CatalogScanServiceResultType.NewStarted)
                                {
                                    _logger.LogInformation("Catalog scan {ScanId} for {DriverType} has now begun.", timedScan.ScanId, driverType);
                                    incompleteCount++;
                                }
                                else if (result.Type == CatalogScanServiceResultType.UnavailableLease)
                                {
                                    _logger.LogInformation(
                                        "The catalog scan {ScanId} for {DriverType} is being processed by another thread.",
                                        timedScan.ScanId,
                                        driverType);
                                    incompleteCount++;
                                }
                                else if (result.Type == CatalogScanServiceResultType.AlreadyStarted)
                                {
                                    if (result.Scan.State != CatalogIndexScanState.Complete)
                                    {
                                        _logger.LogInformation(
                                            "Catalog scan {ScanId} for {DriverType} driver is in the {State} state. Another thread must have started the scan first.",
                                            timedScan.ScanId,
                                            driverType,
                                            result.Scan.State);
                                        incompleteCount++;
                                    }
                                    else
                                    {
                                        timedScan.Completed = true;
                                        await _storageService.ReplaceScanAsync(timedScan);
                                    }
                                }
                                else
                                {
                                    throw new InvalidOperationException($"Unexpected catalog scan service result of type {result.Type} for driver {timedScan.DriverType}.");
                                }
                            }
                        }
                    }

                    if (incompleteCount > 0)
                    {
                        _logger.LogInformation("There are still {Count} catalog scans running.", incompleteCount);

                        message.AttemptCount++;
                        await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount));
                        return;
                    }
                    else
                    {
                        if (i < batches.Count - 1)
                        {
                            _logger.LogInformation("Batch {Index} {DriverTypes} is complete. Checking the next batch of drivers.", i, batch);
                        }
                        else
                        {
                            _logger.LogInformation("Batch {Index} {DriverTypes} is complete. All driver batches are now complete.", i, batch);
                        }
                    }
                }

                _logger.LogInformation("All bucket range catalog scans for timed reprocess run {RunId} are complete.", run.RunId);

                run.State = TimedReprocessState.Finalizing;
                await _storageService.ReplaceRunAsync(run);
            }

            if (run.State == TimedReprocessState.Finalizing)
            {
                _logger.LogInformation(
                    "Timed reprocess run {RunId} is marking bucket ranges {BucketRanges} as processed.",
                    run.RunId,
                    run.BucketRanges);
                await _storageService.MarkBucketsAsProcessedAsync(buckets);

                await _storageService.DeleteOldRunsAsync(run.RunId);

                run.Completed = DateTimeOffset.UtcNow;
                run.State = TimedReprocessState.Complete;
                await _storageService.ReplaceRunAsync(run);
            }
        }
    }
}

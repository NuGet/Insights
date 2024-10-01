// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.LoadBucketedPackage;

namespace NuGet.Insights.Worker
{
    public class CatalogIndexScanMessageProcessor : IMessageProcessor<CatalogIndexScanMessage>
    {
        private readonly CatalogClient _catalogClient;
        private readonly ICatalogScanDriverFactory _driverFactory;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly CatalogScanStorageService _storageService;
        private readonly CatalogScanExpandService _expandService;
        private readonly CursorStorageService _cursorStorageService;
        private readonly CatalogScanService _catalogScanService;
        private readonly TableScanService _tableScanService;
        private readonly FanOutRecoveryService _fanOutRecoveryService;
        private readonly ITelemetryClient _telemetryClient;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ILogger<CatalogIndexScanMessageProcessor> _logger;

        public CatalogIndexScanMessageProcessor(
            CatalogClient catalogClient,
            ICatalogScanDriverFactory driverFactory,
            IMessageEnqueuer messageEnqueuer,
            CatalogScanStorageService storageService,
            CatalogScanExpandService expandService,
            CursorStorageService cursorStorageService,
            CatalogScanService catalogScanService,
            TableScanService tableScanService,
            FanOutRecoveryService fanOutRecoveryService,
            ITelemetryClient telemetryClient,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger<CatalogIndexScanMessageProcessor> logger)
        {
            _catalogClient = catalogClient;
            _driverFactory = driverFactory;
            _messageEnqueuer = messageEnqueuer;
            _storageService = storageService;
            _expandService = expandService;
            _cursorStorageService = cursorStorageService;
            _catalogScanService = catalogScanService;
            _tableScanService = tableScanService;
            _fanOutRecoveryService = fanOutRecoveryService;
            _telemetryClient = telemetryClient;
            _options = options;
            _logger = logger;
        }

        public async Task ProcessAsync(CatalogIndexScanMessage message, long dequeueCount)
        {
            var scan = await _storageService.GetIndexScanAsync(message.DriverType, message.ScanId);
            if (scan is null)
            {
                if (message.AttemptCount < 10)
                {
                    _logger.LogTransientWarning("After {AttemptCount} attempts, the '{DriverType}' catalog index scan '{ScanId}' should have already been created. Trying again.",
                        message.AttemptCount,
                        message.DriverType,
                        message.ScanId);
                    message.AttemptCount++;
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount));
                }
                else
                {
                    _logger.LogTransientWarning("After {AttemptCount} attempts, the '{DriverType}' catalog index scan '{ScanId}' should have already been created. Giving up.",
                        message.AttemptCount,
                        message.DriverType,
                        message.ScanId);
                }

                return;
            }

            using var loggerScope = _logger.BeginScope("Catalog index scan: {Scope_CatalogIndexScanDriverType}", scan.DriverType);

            if (scan.State.IsTerminal())
            {
                return;
            }

            _logger.LogInformation(
                "Processing catalog index scan {ScanId} of type {DriverType}. The current state is {State}.",
                scan.ScanId,
                scan.DriverType,
                scan.State);

            var driver = _driverFactory.Create(scan.DriverType);

            // Created: initialize the storage for the driver and set the started time
            if (scan.State == CatalogIndexScanState.Created)
            {
                await driver.InitializeAsync(scan);

                scan.State = CatalogIndexScanState.Initialized;
                scan.Started = DateTimeOffset.UtcNow;
                if (!await TryReplaceAsync(message, scan))
                {
                    return;
                }

                _telemetryClient
                    .GetMetric("CatalogIndexScan.Count", "DriverType", "ParentDriverType", "RangeType")
                    .TrackValue(1, scan.DriverType.ToString(), scan.ParentDriverType?.ToString() ?? "none", scan.BucketRanges is not null ? "Bucket" : "Commit");
            }

            // Created with null result: determine the index scan result, i.e. the mode and initialize the child tables
            if (scan.State == CatalogIndexScanState.Initialized && !scan.Result.HasValue)
            {
                scan.Result = await driver.ProcessIndexAsync(scan);

                switch (scan.Result.Value)
                {
                    case CatalogIndexScanResult.ExpandAllLeaves:
                        if (scan.OnlyLatestLeaves)
                        {
                            throw new NotSupportedException($"Catalog index scan result '{scan.Result.Value}' is not supported when {nameof(CatalogIndexScan.OnlyLatestLeaves)} is true.");
                        }

                        await _storageService.InitializePageScanTableAsync(scan.StorageSuffix);
                        await _storageService.InitializeLeafScanTableAsync(scan.StorageSuffix);
                        break;
                    case CatalogIndexScanResult.ExpandLatestLeaves:
                    case CatalogIndexScanResult.ExpandLatestLeavesPerId:
                        if (!scan.OnlyLatestLeaves)
                        {
                            throw new NotSupportedException($"Catalog index scan result '{scan.Result.Value}' is not supported when {nameof(CatalogIndexScan.OnlyLatestLeaves)} is false.");
                        }

                        await _storageService.InitializeLeafScanTableAsync(scan.StorageSuffix);
                        break;
                    default:
                        throw new NotSupportedException($"Catalog index scan result '{scan.Result}' is not supported.");
                }

                if (!await TryReplaceAsync(message, scan))
                {
                    return;
                }
            }

            if (scan.BucketRanges is null)
            {
                switch (scan.Result.Value)
                {
                    case CatalogIndexScanResult.ExpandAllLeaves:
                        await ExpandAllLeavesAsync(message, scan, driver);
                        break;
                    case CatalogIndexScanResult.ExpandLatestLeaves:
                        await ExpandLatestLeavesAsync(message, scan, driver, perId: false);
                        break;
                    case CatalogIndexScanResult.ExpandLatestLeavesPerId:
                        await ExpandLatestLeavesAsync(message, scan, driver, perId: true);
                        break;
                    default:
                        throw new NotSupportedException($"Catalog index scan result '{scan.Result}' is not supported.");
                }
            }
            else
            {
                if (scan.Result.Value != CatalogIndexScanResult.ExpandLatestLeaves)
                {
                    throw new NotSupportedException($"Processing bucket ranges is not supported with the {scan.Result.Value} mode.");
                }

                await ExpandBucketRangesAsync(message, scan, driver);
            }
        }

        private async Task ExpandAllLeavesAsync(CatalogIndexScanMessage message, CatalogIndexScan scan, ICatalogScanDriver driver)
        {
            await HandleInitializedStateAsync(message, scan, nextState: CatalogIndexScanState.Expanding);

            // Expanding: create a record for each page
            if (scan.State == CatalogIndexScanState.Expanding)
            {
                await ExpandPagesAsync(scan);

                scan.State = CatalogIndexScanState.Enqueueing;
                if (!await TryReplaceAsync(message, scan))
                {
                    return;
                }
            }

            // Enqueueing: enqueue a message for each page
            if (scan.State == CatalogIndexScanState.Enqueueing)
            {
                var createdPageScans = await _storageService.GetAllPageScansAsync(scan.StorageSuffix, scan.ScanId);
                await EnqueuePagesAsync(createdPageScans);

                scan.State = CatalogIndexScanState.Working;
                message.AttemptCount = 0;
                if (!await TryReplaceAsync(message, scan))
                {
                    return;
                }
            }

            // Requeuing: enqueue a message for the some of the remaining pages and leaves
            if (scan.State == CatalogIndexScanState.Requeuing)
            {
                // requeue a chunk of leaf scans
                await _fanOutRecoveryService.EnqueueUnstartedWorkAsync(
                    take => _storageService.GetUnstartedLeafScansAsync(scan.StorageSuffix, scan.ScanId, take),
                    _expandService.EnqueueLeafScansAsync);

                // requeue a chunk of page scans
                await _fanOutRecoveryService.EnqueueUnstartedWorkAsync(
                    take => _storageService.GetUnstartedPageScansAsync(scan.StorageSuffix, scan.ScanId, take),
                    EnqueuePagesAsync);

                scan.State = CatalogIndexScanState.Working;
                if (!await TryReplaceAsync(message, scan))
                {
                    return;
                }
            }

            // Waiting: wait for the page scans and subsequent leaf scans to complete
            if (scan.State == CatalogIndexScanState.Working)
            {
                if (!await ArePageScansCompleteAsync(scan) || !await AreLeafScansCompleteAsync(scan))
                {
                    if (await _fanOutRecoveryService.ShouldRequeueAsync(
                        scan.Timestamp.Value,
                        typeof(CatalogPageScanMessage),
                        typeof(CatalogLeafScanMessage)))
                    {
                        // FAN OUT RECOVERY: it is possible for page scans from a duplicate catalog index scan message to have been
                        // added after the enqueue state completed. To handle this rare case, we will try to reprocess any page
                        // scans that are still hanging. This prevents us from being stuck in the working state waiting on page
                        // scans that have no corresponding catalog page scan queue message.
                        scan.State = CatalogIndexScanState.Requeuing;
                        message.AttemptCount = 0;
                        if (!await TryReplaceAsync(message, scan))
                        {
                            return;
                        }
                    }

                    message.AttemptCount++;
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount));
                    return;
                }
                else
                {
                    scan.State = CatalogIndexScanState.StartingAggregate;
                    if (!await TryReplaceAsync(message, scan))
                    {
                        return;
                    }
                }
            }

            await HandleAggregateAndFinalizeStatesAsync(message, scan, driver);
        }

        private async Task ExpandLatestLeavesAsync(CatalogIndexScanMessage message, CatalogIndexScan scan, ICatalogScanDriver driver, bool perId)
        {
            var findLatestLeafScanId = _storageService.GenerateFindLatestScanId(scan);
            var enqueueTaskStateKey = new TaskStateKey(scan.StorageSuffix, $"{scan.ScanId}-{TableScanDriverType.EnqueueCatalogLeafScans}", "start");

            await HandleInitializedStateAsync(message, scan, nextState: CatalogIndexScanState.FindingLatest);

            // FindingLatest: start and wait on a "find latest leaves" scan for the range of this parent scan
            if (scan.State == CatalogIndexScanState.FindingLatest)
            {
                var storageSuffix = _storageService.GenerateFindLatestStorageSuffix(scan);
                CatalogIndexScan findLatestScan;
                if (perId)
                {
                    findLatestScan = await _catalogScanService.GetOrStartFindLatestCatalogLeafScanPerIdAsync(
                        scanId: findLatestLeafScanId,
                        storageSuffix,
                        scan.DriverType,
                        scan.ScanId,
                        scan.Min,
                        scan.Max);
                }
                else
                {
                    findLatestScan = await _catalogScanService.GetOrStartFindLatestCatalogLeafScanAsync(
                        scanId: findLatestLeafScanId,
                        storageSuffix,
                        scan.DriverType,
                        scan.ScanId,
                        scan.Min,
                        scan.Max);
                }

                if (!findLatestScan.State.IsTerminal())
                {
                    _logger.LogInformation("Still finding latest catalog leaf scans.");
                    message.AttemptCount++;
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount));
                    return;
                }
                else
                {
                    if (findLatestScan.State != CatalogIndexScanState.Complete)
                    {
                        throw new InvalidOperationException("The find latest scan had an unexpected terminal state: " + findLatestScan.State);
                    }

                    _logger.LogInformation("Finding the latest catalog leaf scans is complete.");

                    scan.State = CatalogIndexScanState.Expanding;
                    if (!await TryReplaceAsync(message, scan))
                    {
                        return;
                    }
                }
            }

            // Expanding: create task state for the table scan of the latest leaves table
            if (scan.State == CatalogIndexScanState.Expanding)
            {
                // Since the find latest leaves catalog scan is complete, delete the record.
                var findLatestLeavesScan = await _storageService.GetIndexScanAsync(
                    perId ? CatalogScanDriverType.Internal_FindLatestCatalogLeafScanPerId : CatalogScanDriverType.Internal_FindLatestCatalogLeafScan,
                    findLatestLeafScanId);
                if (findLatestLeavesScan != null)
                {
                    await _storageService.DeleteAsync(findLatestLeavesScan);
                }

                await _tableScanService.InitializeTaskStateAsync(enqueueTaskStateKey);
                scan.State = CatalogIndexScanState.Enqueueing;
                if (!await TryReplaceAsync(message, scan))
                {
                    return;
                }
            }

            // NOTE: this table scan does not strictly need to be only on partition key. Since we are only
            // writing a single leaf scan per package ID (where leaf scan partition key is scoped at the package
            // ID level), we can simply process all leaf scans. However this is a defensive approach to ensure
            // to ensure the preceding logic is working as expected.
            //
            // Also, I implemented this partition key scanning logic and I want to leverage it here ^_^
            var enqueuePerId = perId;

            await HandleEnqueueAggregateAndFinalizeStatesAsync(message, scan, driver, enqueueTaskStateKey, enqueuePerId);
        }

        private async Task ExpandBucketRangesAsync(CatalogIndexScanMessage message, CatalogIndexScan scan, ICatalogScanDriver driver)
        {
            var copyTaskStatePk = $"{scan.ScanId}-{TableScanDriverType.CopyBucketRange}";
            var enqueueTaskStateKey = new TaskStateKey(scan.StorageSuffix, $"{scan.ScanId}-{TableScanDriverType.EnqueueCatalogLeafScans}", "start");

            // this can't overlap with the table scan task state prefixes
            var bucketRangeRowKeyPrefix = "bucket-range-";

            // Initialized: add task states for the table scans per bucket range
            if (scan.State == CatalogIndexScanState.Initialized)
            {
                // Create task states.
                await _tableScanService.InitializeTaskStatesAsync(
                    scan.StorageSuffix,
                    copyTaskStatePk,
                    BucketRange.ParseRanges(scan.BucketRanges).Select(x => $"{bucketRangeRowKeyPrefix}{x}").ToList());

                scan.State = CatalogIndexScanState.Expanding;
                if (!await TryReplaceAsync(message, scan))
                {
                    return;
                }
            }

            // Expanding: start the table scans which create the leaf scans
            if (scan.State == CatalogIndexScanState.Expanding)
            {
                var bucketRangeTaskStates = await _tableScanService.GetTaskStatesAsync(
                   scan.StorageSuffix,
                   copyTaskStatePk,
                   bucketRangeRowKeyPrefix);

                foreach (var taskState in bucketRangeTaskStates)
                {
                    var range = BucketRange.Parse(taskState.RowKey.Substring(bucketRangeRowKeyPrefix.Length));
                    if (taskState.Message is null)
                    {
                        await _tableScanService.StartCopyBucketRangeAsync(
                            taskState,
                            range.Min,
                            range.Max,
                            scan.DriverType,
                            scan.ScanId);
                    }
                }

                if (!await _tableScanService.IsCompleteAsync(scan.StorageSuffix, copyTaskStatePk))
                {
                    message.AttemptCount++;
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount));
                    return;
                }

                await _tableScanService.InitializeTaskStateAsync(enqueueTaskStateKey);
                scan.State = CatalogIndexScanState.Enqueueing;
                if (!await TryReplaceAsync(message, scan))
                {
                    return;
                }
            }

            await HandleEnqueueAggregateAndFinalizeStatesAsync(message, scan, driver, enqueueTaskStateKey, enqueuePerId: false);
        }

        private async Task<bool> ArePageScansCompleteAsync(CatalogIndexScan scan)
        {
            var countLowerBound = await _storageService.GetPageScanCountLowerBoundAsync(scan.StorageSuffix, scan.ScanId);
            if (countLowerBound > 0)
            {
                _logger.LogInformation("There are at least {Count} page scans pending.", countLowerBound);
                return false;
            }

            return true;
        }

        private async Task<bool> AreLeafScansCompleteAsync(CatalogIndexScan scan)
        {
            var countLowerBound = await _storageService.GetLeafScanCountLowerBoundAsync(
                scan.StorageSuffix,
                scan.ScanId);
            if (countLowerBound > 0)
            {
                _logger.LogInformation("There are at least {Count} leaf scans pending.", countLowerBound);
                return false;
            }

            return true;
        }

        private async Task HandleInitializedStateAsync(CatalogIndexScanMessage message, CatalogIndexScan scan, CatalogIndexScanState nextState)
        {
            // Initialized: set the started timestamp
            if (scan.State == CatalogIndexScanState.Initialized)
            {
                scan.State = nextState;
                scan.Started = DateTimeOffset.UtcNow;
                if (!await TryReplaceAsync(message, scan))
                {
                    return;
                }
            }
        }

        private async Task HandleEnqueueAggregateAndFinalizeStatesAsync(
            CatalogIndexScanMessage message,
            CatalogIndexScan scan,
            ICatalogScanDriver driver,
            TaskStateKey taskStateKey,
            bool enqueuePerId)
        {
            // Enqueueing: start the table scan of the latest leaves table
            if (scan.State == CatalogIndexScanState.Enqueueing)
            {
                await _tableScanService.StartEnqueueCatalogLeafScansAsync(
                    taskStateKey,
                    _storageService.GetLeafScanTableName(scan.StorageSuffix),
                    oneMessagePerId: enqueuePerId);

                scan.State = CatalogIndexScanState.Working;
                message.AttemptCount = 0;
                if (!await TryReplaceAsync(message, scan))
                {
                    return;
                }
            }

            // Working: wait for the table scan and subsequent leaf scans to complete
            if (scan.State == CatalogIndexScanState.Working)
            {
                if (!await _tableScanService.IsCompleteAsync(taskStateKey.StorageSuffix, taskStateKey.PartitionKey) || !await AreLeafScansCompleteAsync(scan))
                {
                    message.AttemptCount++;
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount));
                    return;
                }
                else
                {
                    scan.State = CatalogIndexScanState.StartingAggregate;
                    if (!await TryReplaceAsync(message, scan))
                    {
                        return;
                    }
                }
            }

            await HandleAggregateAndFinalizeStatesAsync(message, scan, driver);
        }

        private async Task HandleAggregateAndFinalizeStatesAsync(CatalogIndexScanMessage message, CatalogIndexScan scan, ICatalogScanDriver driver)
        {
            // StartingAggregate: call into the driver to start aggregating.
            if (scan.State == CatalogIndexScanState.StartingAggregate)
            {
                await driver.StartAggregateAsync(scan);

                scan.State = CatalogIndexScanState.Aggregating;
                message.AttemptCount = 0;
                if (!await TryReplaceAsync(message, scan))
                {
                    return;
                }
            }

            // Aggregating: wait for the aggregation step is complete
            if (scan.State == CatalogIndexScanState.Aggregating)
            {
                if (!await driver.IsAggregateCompleteAsync(scan))
                {
                    _logger.LogInformation("The index scan is still aggregating.");
                    message.AttemptCount++;
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount));
                    return;
                }
                else
                {
                    scan.State = CatalogIndexScanState.Finalizing;
                    if (!await TryReplaceAsync(message, scan))
                    {
                        return;
                    }
                }
            }

            // Finalizing: perform clean-up steps
            if (scan.State == CatalogIndexScanState.Finalizing)
            {
                // Finalize the driver.
                await driver.FinalizeAsync(scan);

                // Delete child tables, but only if a storage suffix is used.
                if (!string.IsNullOrEmpty(scan.StorageSuffix))
                {
                    _logger.LogInformation("Deleting suffixed scan state tables.");
                    await _storageService.DeleteChildTablesAsync(scan.StorageSuffix);
                    await _tableScanService.DeleteTaskStateTableAsync(scan.StorageSuffix);
                }

                // Update the cursor, now that the work is done.
                if (scan.CursorName != CatalogScanService.NoCursor)
                {
                    if (scan.BucketRanges is not null)
                    {
                        throw new InvalidOperationException("A cursor update is not allow when using bucket ranges.");
                    }

                    var cursor = await _cursorStorageService.GetOrCreateAsync(scan.CursorName);
                    if (cursor.Value < scan.Max)
                    {
                        cursor.Value = scan.Max;
                        await _cursorStorageService.UpdateAsync(cursor);
                    }
                    else
                    {
                        _logger.LogTransientWarning(
                            "The cursor {CursorName} already has a value of {ExistingValue:O}. Skipping an update of {ScanValue:O}.",
                            cursor.Name,
                            cursor.Value,
                            scan.Max);
                    }
                }

                // Continue the update, if directed.
                if (scan.ContinueUpdate)
                {
                    await _catalogScanService.UpdateAllAsync(scan.Max);
                }

                // Delete old scans if this scan has no parents. If this scan has a parent it will be cleaned up by its parent instead.
                if (!scan.ParentDriverType.HasValue)
                {
                    if (scan.ParentScanId is not null)
                    {
                        throw new InvalidOperationException("The parent scan ID is set but there is not parent driver type.");
                    }

                    await _storageService.DeleteOldIndexScansAsync(scan.DriverType, scan.ScanId);
                }

                _logger.LogInformation("The catalog scan is complete.");
                await CompleteAsync(message, scan);
            }
        }

        private async Task CompleteAsync(CatalogIndexScanMessage message, CatalogIndexScan scan)
        {
            scan.State = CatalogIndexScanState.Complete;
            scan.Completed = DateTimeOffset.UtcNow;
            if (!await TryReplaceAsync(message, scan))
            {
                return;
            }
        }

        private async Task<bool> TryReplaceAsync(CatalogIndexScanMessage message, CatalogIndexScan scan)
        {
            var result = await _storageService.TryReplaceAsync(scan);
            switch (result.Type)
            {
                case EntityChangeResultType.Success:
                    return true;

                case EntityChangeResultType.PreconditionFailed:
                    // If this happens, there was likely a lease timeout on the message, causing another thread
                    // to process this catalog index scan, leading to an etag mismatch on this thread. Without further
                    // coordination, we can't be sure the other thread will properly continue work in another message,
                    // so we'll complete this message and enqueue another one in the future. This can lead to duplicated
                    // work but this is better than dropping a message before the catalog index scan is complete.

                    message.AttemptCount += 60; // artificially increase the attempt count to delay retries
                    var messageDelay = StorageUtility.GetMessageDelay(message.AttemptCount);
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, messageDelay);
                    _logger.LogTransientWarning("Catalog index scan {ScanId} was updated by another actor. The message will be tried again in {MessageDelay}.", scan.ScanId, messageDelay);
                    return false;

                default:
                    throw new NotImplementedException();
            }
        }

        private async Task<CatalogIndex> GetCatalogIndexAsync()
        {
            _logger.LogInformation("Loading catalog index.");
            var catalogIndex = await _catalogClient.GetCatalogIndexAsync();
            return catalogIndex;
        }

        private async Task<List<CatalogPageScan>> CreatePageScansAsync(CatalogIndexScan scan)
        {
            var catalogIndex = await GetCatalogIndexAsync();
            var pageItemToRank = catalogIndex.GetPageItemToRank();

            var pages = catalogIndex.GetPagesInBounds(scan.Min, scan.Max);

            _logger.LogInformation(
                "Starting {DriverType} scan of {PageCount} pages from ({Min:O}, {Max:O}].",
                scan.DriverType,
                pages.Count,
                scan.Min,
                scan.Max);

            return pages
                .OrderBy(x => pageItemToRank[x])
                .Select(x => CreatePageScan(scan, x.Url, pageItemToRank[x]))
                .ToList();
        }

        private CatalogPageScan CreatePageScan(CatalogIndexScan scan, string url, int rank)
        {
            return new CatalogPageScan(
                scan.StorageSuffix,
                scan.ScanId,
                "P" + rank.ToString(CultureInfo.InvariantCulture).PadLeft(10, '0'))
            {
                DriverType = scan.DriverType,
                OnlyLatestLeaves = scan.OnlyLatestLeaves,
                ParentDriverType = scan.ParentDriverType,
                ParentScanId = scan.ParentScanId,
                State = CatalogPageScanState.Created,
                Min = scan.Min,
                Max = scan.Max,
                BucketRanges = scan.BucketRanges,
                Url = url,
                Rank = rank,
            };
        }

        private async Task ExpandPagesAsync(CatalogIndexScan scan)
        {
            var allPageScans = await CreatePageScansAsync(scan);

            var createdPageScans = await _storageService.GetAllPageScansAsync(scan.StorageSuffix, scan.ScanId);
            var allUrls = allPageScans.Select(x => x.Url).ToHashSet();
            var createdUrls = createdPageScans.Select(x => x.Url).ToHashSet();
            var uncreatedUrls = allUrls.Except(createdUrls).ToHashSet();

            if (createdUrls.Except(allUrls).Any())
            {
                throw new InvalidOperationException("There should not be any extra page scan entities.");
            }

            var uncreatedPageScans = allPageScans
                .Where(x => uncreatedUrls.Contains(x.Url))
                .ToList();
            _logger.LogInformation("Inserting {PageCount} pages. {ExistinCount} are already created.", uncreatedPageScans.Count, createdUrls.Count);
            await _storageService.InsertAsync(uncreatedPageScans);
        }

        private async Task EnqueuePagesAsync(IReadOnlyList<CatalogPageScan> createdPageScans)
        {
            _logger.LogInformation("Enqueueing a scan of {PageCount} pages.", createdPageScans.Count);
            await _messageEnqueuer.EnqueueAsync(
                createdPageScans
                    .Select(x => new CatalogPageScanMessage
                    {
                        StorageSuffix = x.StorageSuffix,
                        ScanId = x.ScanId,
                        PageId = x.PageId,
                    })
                    .ToList());
        }
    }
}

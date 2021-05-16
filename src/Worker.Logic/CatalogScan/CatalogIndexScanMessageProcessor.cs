using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogIndexScanMessageProcessor : IMessageProcessor<CatalogIndexScanMessage>
    {
        private readonly CatalogClient _catalogClient;
        private readonly ICatalogScanDriverFactory _driverFactory;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly CatalogScanStorageService _storageService;
        private readonly CursorStorageService _cursorStorageService;
        private readonly CatalogScanService _catalogScanService;
        private readonly TaskStateStorageService _taskStateStorageService;
        private readonly TableScanService<CatalogLeafScan> _tableScanService;
        private readonly ILogger<CatalogIndexScanMessageProcessor> _logger;

        public CatalogIndexScanMessageProcessor(
            CatalogClient catalogClient,
            ICatalogScanDriverFactory driverFactory,
            IMessageEnqueuer messageEnqueuer,
            CatalogScanStorageService storageService,
            CursorStorageService cursorStorageService,
            CatalogScanService catalogScanService,
            TaskStateStorageService taskStateStorageService,
            TableScanService<CatalogLeafScan> tableScanService,
            ILogger<CatalogIndexScanMessageProcessor> logger)
        {
            _catalogClient = catalogClient;
            _driverFactory = driverFactory;
            _messageEnqueuer = messageEnqueuer;
            _storageService = storageService;
            _cursorStorageService = cursorStorageService;
            _catalogScanService = catalogScanService;
            _taskStateStorageService = taskStateStorageService;
            _tableScanService = tableScanService;
            _logger = logger;
        }

        public async Task ProcessAsync(CatalogIndexScanMessage message, long dequeueCount)
        {
            var scan = await _storageService.GetIndexScanAsync(message.CursorName, message.ScanId);
            if (scan == null)
            {
                await Task.Delay(TimeSpan.FromSeconds(dequeueCount * 15));
                throw new InvalidOperationException($"The catalog index scan '{message.ScanId}' with cursor '{message.CursorName}' should have already been created.");
            }

            var driver = _driverFactory.Create(scan.DriverType);

            // Created: initialize the storage for the driver and set the started time
            if (scan.State == CatalogIndexScanState.Created)
            {
                await driver.InitializeAsync(scan);

                scan.State = CatalogIndexScanState.Initialized;
                scan.Started = DateTimeOffset.UtcNow;
                await _storageService.ReplaceAsync(scan);
            }

            // Created with null result: determine the index scan result, i.e. the mode and initialize the child tables
            if (scan.State == CatalogIndexScanState.Initialized && !scan.GetResult().HasValue)
            {
                scan.SetResult(await driver.ProcessIndexAsync(scan));

                switch (scan.GetResult().Value)
                {
                    case CatalogIndexScanResult.ExpandAllLeaves:
                        await _storageService.InitializePageScanTableAsync(scan.StorageSuffix);
                        await _storageService.InitializeLeafScanTableAsync(scan.StorageSuffix);
                        break;
                    case CatalogIndexScanResult.ExpandLatestLeaves:
                    case CatalogIndexScanResult.ExpandLatestLeavesPerId:
                    case CatalogIndexScanResult.CustomExpand:
                        await _storageService.InitializeLeafScanTableAsync(scan.StorageSuffix);
                        break;
                    case CatalogIndexScanResult.Processed:
                        break;
                    default:
                        throw new NotSupportedException($"Catalog index scan result '{scan.GetResult()}' is not supported.");
                }

                await _storageService.ReplaceAsync(scan);
            }

            switch (scan.GetResult().Value)
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
                case CatalogIndexScanResult.CustomExpand:
                    await CustomExpandAsync(message, scan, driver);
                    break;
                case CatalogIndexScanResult.Processed:
                    await CompleteAsync(scan);
                    break;
                default:
                    throw new NotSupportedException($"Catalog index scan result '{scan.GetResult()}' is not supported.");
            }
        }

        private async Task ExpandAllLeavesAsync(CatalogIndexScanMessage message, CatalogIndexScan scan, ICatalogScanDriver driver)
        {
            var lazyIndexTask = await HandleInitializedStateAsync(scan, nextState: CatalogIndexScanState.Expanding);

            var lazyPageScansTask = new Lazy<Task<List<CatalogPageScan>>>(async () => GetPageScans(scan, await lazyIndexTask.Value));

            // Expanding: create a record for each page
            if (scan.State == CatalogIndexScanState.Expanding)
            {
                var pageScans = await lazyPageScansTask.Value;
                await ExpandAsync(scan, pageScans);

                scan.State = CatalogIndexScanState.Enqueuing;
                await _storageService.ReplaceAsync(scan);
            }

            // Enqueueing: enqueue a message for each page
            if (scan.State == CatalogIndexScanState.Enqueuing)
            {
                var pageScans = await lazyPageScansTask.Value;
                await EnqueueAsync(pageScans);

                scan.State = CatalogIndexScanState.Working;
                message.AttemptCount = 0;
                await _storageService.ReplaceAsync(scan);
            }

            // Waiting: wait for the page scans and subsequent leaf scans to complete
            if (scan.State == CatalogIndexScanState.Working)
            {
                if (!await ArePageScansCompleteAsync(scan) || !await AreLeafScansCompleteAsync(scan))
                {
                    message.AttemptCount++;
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount));
                    return;
                }
                else
                {
                    scan.State = CatalogIndexScanState.StartingAggregate;
                    await _storageService.ReplaceAsync(scan);
                }
            }

            await HandleAggregateAndFinalizeStatesAsync(message, scan, driver);
        }

        private async Task ExpandLatestLeavesAsync(CatalogIndexScanMessage message, CatalogIndexScan scan, ICatalogScanDriver driver, bool perId)
        {
            var findLatestLeafScanId = scan.GetScanId() + "-fl";
            var taskStateKey = new TaskStateKey(scan.StorageSuffix, $"{scan.GetScanId()}-{TableScanDriverType.EnqueueCatalogLeafScans}", "start");

            await HandleInitializedStateAsync(scan, nextState: CatalogIndexScanState.FindingLatest);

            // FindingLatest: start and wait on a "find latest leaves" scan for the range of this parent scan
            if (scan.State == CatalogIndexScanState.FindingLatest)
            {
                var storageSuffix = scan.StorageSuffix + "fl";
                CatalogIndexScan findLatestScan;
                if (perId)
                {
                    findLatestScan = await _catalogScanService.GetOrStartFindLatestCatalogLeafScanPerIdAsync(
                        scanId: findLatestLeafScanId,
                        storageSuffix,
                        parentScanMessage: message,
                        scan.Min.Value,
                        scan.Max.Value);
                }
                else
                {
                    findLatestScan = await _catalogScanService.GetOrStartFindLatestCatalogLeafScanAsync(
                        scanId: findLatestLeafScanId,
                        storageSuffix,
                        parentScanMessage: message,
                        scan.Min.Value,
                        scan.Max.Value);
                }

                if (findLatestScan.State != CatalogIndexScanState.Complete)
                {
                    _logger.LogInformation("Still finding latest catalog leaf scans.");
                    message.AttemptCount++;
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount));
                    return;
                }
                else
                {
                    _logger.LogInformation("Finding the latest catalog leaf scans is complete.");

                    scan.State = CatalogIndexScanState.Expanding;
                    await _storageService.ReplaceAsync(scan);
                }
            }

            // Expanding: create task state for the table scan of the latest leaves table
            if (scan.State == CatalogIndexScanState.Expanding)
            {
                // Since the find latest leaves catalog scan is complete, delete the record.
                var findLatestLeavesScan = await _storageService.GetIndexScanAsync(cursorName: string.Empty, findLatestLeafScanId);
                if (findLatestLeavesScan != null)
                {
                    await _storageService.DeleteAsync(findLatestLeavesScan);
                }

                await _taskStateStorageService.InitializeAsync(scan.StorageSuffix);
                await _taskStateStorageService.AddAsync(taskStateKey);
                scan.State = CatalogIndexScanState.Enqueuing;
                await _storageService.ReplaceAsync(scan);
            }

            // NOTE: this table scan does not strictly need to be only on partition key. Since we are only
            // writing a single leaf scan per package ID (where leaf scan partition key is scoped at the package
            // ID level), we can simply process all leaf scans. However this is a defensive approach to ensure
            // to ensure the preceding logic is working as expected.
            //
            // Also, I implemented this partition key scanning logic and I want to leverage it here ^_^
            var enqueuePerId = perId;

            await HandleEnqueueAggregateAndFinalizeStatesAsync(message, scan, driver, taskStateKey, enqueuePerId);
        }

        private async Task CustomExpandAsync(CatalogIndexScanMessage message, CatalogIndexScan scan, ICatalogScanDriver driver)
        {
            var taskStateKey = new TaskStateKey(scan.StorageSuffix, $"{scan.GetScanId()}-{TableScanDriverType.EnqueueCatalogLeafScans}", "start");

            await HandleInitializedStateAsync(scan, nextState: CatalogIndexScanState.StartingExpand);

            // StartingExpand: start the custom expand flow provided by the driver
            if (scan.State == CatalogIndexScanState.StartingExpand)
            {
                await driver.StartCustomExpandAsync(scan);

                scan.State = CatalogIndexScanState.Expanding;
                await _storageService.ReplaceAsync(scan);
            }

            // Expanding: wait for the custom expand flow to complete
            if (scan.State == CatalogIndexScanState.Expanding)
            {
                if (!await driver.IsCustomExpandCompleteAsync(scan))
                {
                    _logger.LogInformation("The custom expand is still running.");
                    message.AttemptCount++;
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount));
                    return;
                }
                else
                {
                    await _taskStateStorageService.InitializeAsync(scan.StorageSuffix);
                    await _taskStateStorageService.AddAsync(taskStateKey);
                    scan.State = CatalogIndexScanState.Enqueuing;
                    await _storageService.ReplaceAsync(scan);
                }
            }

            // NOTE: we never need to enqueue per ID since the custom expand logic can create any granularity of leaf
            // item that it wants.
            var enqueuePerId = false;
            await HandleEnqueueAggregateAndFinalizeStatesAsync(message, scan, driver, taskStateKey, enqueuePerId);
        }

        private async Task<bool> ArePageScansCompleteAsync(CatalogIndexScan scan)
        {
            var countLowerBound = await _storageService.GetPageScanCountLowerBoundAsync(scan.StorageSuffix, scan.GetScanId());
            if (countLowerBound > 0)
            {
                _logger.LogInformation("There are at least {Count} page scans pending.", countLowerBound);
                return false;
            }

            return true;
        }

        private async Task<bool> AreTableScanStepsCompleteAsync(TaskStateKey taskStateKey)
        {
            var taskStateCountLowerBound = await _taskStateStorageService.GetCountLowerBoundAsync(
                taskStateKey.StorageSuffix,
                taskStateKey.PartitionKey);
            if (taskStateCountLowerBound > 0)
            {
                _logger.LogInformation("There are at least {Count} table scan steps pending.", taskStateCountLowerBound);
                return false;
            }

            return true;
        }

        private async Task<bool> AreLeafScansCompleteAsync(CatalogIndexScan scan)
        {
            var countLowerBound = await _storageService.GetLeafScanCountLowerBoundAsync(
                scan.StorageSuffix,
                scan.GetScanId());
            if (countLowerBound > 0)
            {
                _logger.LogInformation("There are at least {Count} leaf scans pending.", countLowerBound);
                return false;
            }

            return true;
        }

        private async Task<Lazy<Task<CatalogIndex>>> HandleInitializedStateAsync(CatalogIndexScan scan, CatalogIndexScanState nextState)
        {
            var lazyIndexTask = new Lazy<Task<CatalogIndex>>(() => GetCatalogIndexAsync());

            // Initialized: determine the real time bounds for the scan.
            if (scan.State == CatalogIndexScanState.Initialized)
            {
                var catalogIndex = await lazyIndexTask.Value;

                var min = scan.Min ?? CatalogClient.NuGetOrgMin;
                var max = new[] { scan.Max ?? DateTimeOffset.MaxValue, catalogIndex.CommitTimestamp }.Min();
                if (scan.Min != min || scan.Max != max)
                {
                    scan.Min = min;
                    scan.Max = max;
                    await _storageService.ReplaceAsync(scan);
                }

                scan.State = nextState;
                scan.Started = DateTimeOffset.UtcNow;
                await _storageService.ReplaceAsync(scan);
            }

            return lazyIndexTask;
        }

        private async Task HandleEnqueueAggregateAndFinalizeStatesAsync(
            CatalogIndexScanMessage message,
            CatalogIndexScan scan,
            ICatalogScanDriver driver,
            TaskStateKey taskStateKey,
            bool enqueuePerId)
        {
            // Enqueueing: start the table scan of the latest leaves table
            if (scan.State == CatalogIndexScanState.Enqueuing)
            {
                await _taskStateStorageService.AddAsync(taskStateKey);
                await _tableScanService.StartEnqueueCatalogLeafScansAsync(
                    taskStateKey,
                    _storageService.GetLeafScanTableName(scan.StorageSuffix),
                    oneMessagePerId: enqueuePerId);

                scan.State = CatalogIndexScanState.Working;
                message.AttemptCount = 0;
                await _storageService.ReplaceAsync(scan);
            }

            // Waiting: wait for the table scan and subsequent leaf scans to complete
            if (scan.State == CatalogIndexScanState.Working)
            {
                if (!await AreTableScanStepsCompleteAsync(taskStateKey) || !await AreLeafScansCompleteAsync(scan))
                {
                    message.AttemptCount++;
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount));
                    return;
                }
                else
                {
                    scan.State = CatalogIndexScanState.StartingAggregate;
                    await _storageService.ReplaceAsync(scan);
                }
            }

            await HandleAggregateAndFinalizeStatesAsync(message, scan, driver);
        }

        private async Task HandleAggregateAndFinalizeStatesAsync(CatalogIndexScanMessage message, CatalogIndexScan scan, ICatalogScanDriver driver)
        {
            // StartAggregating: call into the driver to start aggregating.
            if (scan.State == CatalogIndexScanState.StartingAggregate)
            {
                await driver.StartAggregateAsync(scan);

                scan.State = CatalogIndexScanState.Aggregating;
                message.AttemptCount = 0;
                await _storageService.ReplaceAsync(scan);
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
                    await _storageService.ReplaceAsync(scan);
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
                    await _taskStateStorageService.DeleteTableAsync(scan.StorageSuffix);
                }

                // Update the cursor, now that the work is done.
                if (scan.GetCursorName() != string.Empty)
                {
                    var cursor = await _cursorStorageService.GetOrCreateAsync(scan.GetCursorName());
                    if (cursor.Value <= scan.Max.Value)
                    {
                        cursor.Value = scan.Max.Value;
                        await _cursorStorageService.UpdateAsync(cursor);
                    }
                }

                // Continue the update, if directed.
                if (scan.ContinueUpdate)
                {
                    await _catalogScanService.UpdateAllAsync(scan.Max.Value);
                }

                // Delete old scans
                await _storageService.DeleteOldIndexScansAsync(scan.GetCursorName(), scan.GetScanId());

                _logger.LogInformation("The catalog scan is complete.");
                await CompleteAsync(scan);
            }
        }

        private async Task CompleteAsync(CatalogIndexScan scan)
        {
            scan.State = CatalogIndexScanState.Complete;
            scan.Completed = DateTimeOffset.UtcNow;
            await _storageService.ReplaceAsync(scan);
        }

        private async Task<CatalogIndex> GetCatalogIndexAsync()
        {
            _logger.LogInformation("Loading catalog index.");
            var catalogIndex = await _catalogClient.GetCatalogIndexAsync();
            return catalogIndex;
        }

        private List<CatalogPageScan> GetPageScans(CatalogIndexScan scan, CatalogIndex catalogIndex)
        {
            var pageItemToRank = catalogIndex.GetPageItemToRank();

            var pages = catalogIndex.GetPagesInBounds(scan.Min.Value, scan.Max.Value);

            _logger.LogInformation(
                "Starting {DriverType} scan of {PageCount} pages from ({Min:O}, {Max:O}].",
                scan.DriverType,
                pages.Count,
                scan.Min.Value,
                scan.Max.Value);

            return pages
                .OrderBy(x => pageItemToRank[x])
                .Select(x => CreatePageScan(scan, x.Url, pageItemToRank[x]))
                .ToList();
        }

        private CatalogPageScan CreatePageScan(CatalogIndexScan scan, string url, int rank)
        {
            return new CatalogPageScan(
                scan.StorageSuffix,
                scan.GetScanId(),
                "P" + rank.ToString(CultureInfo.InvariantCulture).PadLeft(10, '0'))
            {
                DriverType = scan.DriverType,
                DriverParameters = scan.DriverParameters,
                State = CatalogPageScanState.Created,
                Min = scan.Min.Value,
                Max = scan.Max.Value,
                Url = url,
                Rank = rank,
            };
        }

        private async Task ExpandAsync(CatalogIndexScan scan, List<CatalogPageScan> allPageScans)
        {
            var createdPageScans = await _storageService.GetPageScansAsync(scan.StorageSuffix, scan.GetScanId());
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
            await _storageService.InsertAsync(uncreatedPageScans);
        }

        private async Task EnqueueAsync(List<CatalogPageScan> pageScans)
        {
            _logger.LogInformation("Enqueuing a scan of {PageCount} pages.", pageScans.Count);
            await _messageEnqueuer.EnqueueAsync(
                pageScans
                    .Select(x => new CatalogPageScanMessage
                    {
                        StorageSuffix = x.StorageSuffix,
                        ScanId = x.GetScanId(),
                        PageId = x.GetPageId(),
                    })
                    .ToList());
        }
    }
}

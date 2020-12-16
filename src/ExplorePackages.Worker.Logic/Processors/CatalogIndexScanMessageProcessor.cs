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
        private readonly CatalogScanDriverFactory _driverFactory;
        private readonly MessageEnqueuer _messageEnqueuer;
        private readonly CatalogScanStorageService _storageService;
        private readonly CursorStorageService _cursorStorageService;
        private readonly ILogger<CatalogIndexScanMessageProcessor> _logger;

        public CatalogIndexScanMessageProcessor(
            CatalogClient catalogClient,
            CatalogScanDriverFactory driverFactory,
            MessageEnqueuer messageEnqueuer,
            CatalogScanStorageService storageService,
            CursorStorageService cursorStorageService,
            ILogger<CatalogIndexScanMessageProcessor> logger)
        {
            _catalogClient = catalogClient;
            _driverFactory = driverFactory;
            _messageEnqueuer = messageEnqueuer;
            _storageService = storageService;
            _cursorStorageService = cursorStorageService;
            _logger = logger;
        }

        public async Task ProcessAsync(CatalogIndexScanMessage message)
        {
            var scan = await _storageService.GetIndexScanAsync(message.CursorName, message.ScanId);
            if (scan == null)
            {
                await Task.Delay(TimeSpan.FromSeconds(10));
                throw new InvalidOperationException("The catalog index scan should have already been created.");
            }

            var driver = _driverFactory.Create(scan.ParsedScanType);

            var result = await driver.ProcessIndexAsync(scan);

            switch (result)
            {
                case CatalogIndexScanResult.Expand:
                    await ExpandAsync(message, scan, driver);
                    break;
                case CatalogIndexScanResult.Processed:
                    break;
                default:
                    throw new NotSupportedException($"Catalog index scan result '{result}' is not supported.");
            }
        }

        private async Task ExpandAsync(CatalogIndexScanMessage message, CatalogIndexScan scan, ICatalogScanDriver driver)
        {
            var lazyIndexTask = new Lazy<Task<CatalogIndex>>(() => GetCatalogIndexAsync());
            var lazyPageScansTask = new Lazy<Task<List<CatalogPageScan>>>(async () => GetPageScans(scan, await lazyIndexTask.Value));

            // Created: determine the real time bounds for the scan.
            if (scan.ParsedState == CatalogScanState.Created)
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

                scan.ParsedState = CatalogScanState.Expanding;
                await _storageService.ReplaceAsync(scan);
            }

            // Expanding: create a record for each page
            if (scan.ParsedState == CatalogScanState.Expanding)
            {
                var pageScans = await lazyPageScansTask.Value;
                await ExpandAsync(scan, pageScans);

                scan.ParsedState = CatalogScanState.Enqueuing;
                await _storageService.ReplaceAsync(scan);
            }

            // Enqueueing: enqueue a maessage for each page
            if (scan.ParsedState == CatalogScanState.Enqueuing)
            {
                var pageScans = await lazyPageScansTask.Value;
                await EnqueueAsync(pageScans);

                scan.ParsedState = CatalogScanState.Waiting;
                message.AttemptCount = 0;
                await _storageService.ReplaceAsync(scan);
            }

            // Waiting: check if all of the page scans are complete
            if (scan.ParsedState == CatalogScanState.Waiting)
            {
                var countLowerBound = await _storageService.GetPageScanCountLowerBoundAsync(scan.StorageSuffix, scan.ScanId);
                if (countLowerBound > 0)
                {
                    _logger.LogInformation("There are at least {Count} page scans pending.", countLowerBound);
                    message.AttemptCount++;
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount));
                }
                else
                {
                    scan.ParsedState = CatalogScanState.StartingAggregate;
                    await _storageService.ReplaceAsync(scan);
                }
            }

            // StartAggregating: call into the driver to start aggregating.
            if (scan.ParsedState == CatalogScanState.StartingAggregate)
            {
                await driver.StartAggregateAsync(scan);

                scan.ParsedState = CatalogScanState.Aggregating;
                message.AttemptCount = 0;
                await _storageService.ReplaceAsync(scan);
            }

            // Aggregating: wait for the aggregation step is complete
            if (scan.ParsedState == CatalogScanState.Aggregating)
            {
                if (!await driver.IsAggregateCompleteAsync(scan))
                {
                    _logger.LogInformation("The index scan is still aggregating.");
                    message.AttemptCount++;
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount));
                }
                else
                {
                    scan.ParsedState = CatalogScanState.Finalizing;
                    await _storageService.ReplaceAsync(scan);
                }
            }

            // Finalizing: perform clean-up steps
            if (scan.ParsedState == CatalogScanState.Finalizing)
            {
                // Finalize the driver.
                await driver.FinalizeAsync(scan);

                // Delete child tables, but only if a storage suffix is used.
                if (!string.IsNullOrEmpty(scan.StorageSuffix))
                {
                    _logger.LogInformation("Deleting suffixed scan state tables.");
                    await _storageService.DeleteChildTablesAsync(scan.StorageSuffix);
                }

                // Update the cursor, now that the work is done.
                var cursor = await _cursorStorageService.GetOrCreateAsync(scan.CursorName);
                if (cursor.Value <= scan.Max.Value)
                {
                    cursor.Value = scan.Max.Value;
                    await _cursorStorageService.UpdateAsync(cursor);
                }

                _logger.LogInformation("The catalog scan is complete.");

                scan.ParsedState = CatalogScanState.Complete;
                await _storageService.ReplaceAsync(scan);
            }
        }

        private async Task<CatalogIndex> GetCatalogIndexAsync()
        {
            _logger.LogInformation("Loading catalog index.");
            var catalogIndex = await _catalogClient.GetCatalogIndexAsync();
            return catalogIndex;
        }

        private List<CatalogPageScan> GetPageScans(CatalogIndexScan scan, CatalogIndex catalogIndex)
        {
            var pages = catalogIndex.GetPagesInBounds(scan.Min.Value, scan.Max.Value);

            _logger.LogInformation(
                "Starting {ScanType} scan of {PageCount} pages from ({Min:O}, {Max:O}].",
                scan.ScanType,
                pages.Count,
                scan.Min.Value,
                scan.Max.Value);

            var maxPageIdLength = (pages.Count - 1).ToString().Length;

            var pageScans = pages
                .OrderBy(x => x.CommitTimestamp)
                .Select((x, index) => new CatalogPageScan(
                    scan.StorageSuffix,
                    scan.ScanId,
                    index.ToString(CultureInfo.InvariantCulture).PadLeft(maxPageIdLength, '0'))
                {
                    ParsedScanType = scan.ParsedScanType,
                    ScanParameters = scan.ScanParameters,
                    ParsedState = CatalogScanState.Created,
                    Min = scan.Min.Value,
                    Max = scan.Max.Value,
                    Url = x.Url,
                })
                .ToList();
            return pageScans;
        }

        private async Task ExpandAsync(CatalogIndexScan scan, List<CatalogPageScan> allPageScans)
        {
            var createdPageScans = await _storageService.GetPageScansAsync(scan.StorageSuffix, scan.ScanId);
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
            await _messageEnqueuer.EnqueueAsync(pageScans
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

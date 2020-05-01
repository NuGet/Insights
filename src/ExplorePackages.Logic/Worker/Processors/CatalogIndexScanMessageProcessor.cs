using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class CatalogIndexScanMessageProcessor : IMessageProcessor<CatalogIndexScanMessage>
    {
        private readonly CatalogClient _catalogClient;
        private readonly MessageEnqueuer _messageEnqueuer;
        private readonly CatalogScanStorageService _storageService;
        private readonly ILogger<CatalogIndexScanMessageProcessor> _logger;

        public CatalogIndexScanMessageProcessor(
            CatalogClient catalogClient,
            MessageEnqueuer messageEnqueuer,
            CatalogScanStorageService storageService,
            ILogger<CatalogIndexScanMessageProcessor> logger)
        {
            _catalogClient = catalogClient;
            _messageEnqueuer = messageEnqueuer;
            _storageService = storageService;
            _logger = logger;
        }

        public async Task ProcessAsync(CatalogIndexScanMessage message)
        {
            var scan = await _storageService.GetIndexScanAsync(message.ScanId);
            if (scan == null)
            {
                throw new InvalidOperationException("The catalog index scan should have already been created.");
            }

            var lazyCatalogIndexTask = new Lazy<Task<CatalogIndex>>(() => GetCatalogIndexAsync());
            var lazyPageScansTask = new Lazy<Task<List<CatalogPageScan>>>(async () => GetPageScans(scan, await lazyCatalogIndexTask.Value));

            // Created -> Scoped: determine the real time bounds that we'll be using
            if (scan.ParsedState == CatalogIndexScanState.Created)
            {
                var catalogIndex = await lazyCatalogIndexTask.Value;

                scan.Min = scan.Min ?? CursorService.NuGetOrgMin;
                scan.Max = new[] { scan.Max ?? DateTimeOffset.MaxValue, catalogIndex.CommitTimestamp }.Min();
                scan.ParsedState = CatalogIndexScanState.Scoped;
                await _storageService.UpdateIndexScanAsync(scan);
            }

            // Scoped -> Expanded: create a scan entity for each page
            if (scan.ParsedState == CatalogIndexScanState.Scoped)
            {
                var createdPages = await _storageService.GetPageScansAsync(scan.ScanId);
                var pageScans = await lazyPageScansTask.Value;

                var allUrls = pageScans.Select(x => x.Url).ToHashSet();
                var createdUrls = createdPages.Select(x => x.Url).ToHashSet();
                var uncreatedUrls = allUrls.Except(createdUrls).ToHashSet();

                if (createdUrls.Except(allUrls).Any())
                {
                    throw new InvalidOperationException("There should not be any extra page scan entities.");
                }

                var uncreatedPages = pageScans
                    .Where(x => uncreatedUrls.Contains(x.Url))
                    .ToList();
                await _storageService.AddPageScansAsync(uncreatedPages);

                scan.ParsedState = CatalogIndexScanState.Expanded;
                await _storageService.UpdateIndexScanAsync(scan);
            }

            // Expanded -> Enqueued: enqueue a scan message for each page
            if (scan.ParsedState == CatalogIndexScanState.Expanded)
            {
                var pageScans = await lazyPageScansTask.Value;

                _logger.LogInformation(
                    "Starting {ScanType} scan of {PageCount} pages from ({Min:O}, {Max:O}].",
                    scan.ScanType,
                    pageScans.Count,
                    scan.Min.Value,
                    scan.Max.Value);

                await _messageEnqueuer.EnqueueAsync(pageScans.Select(x => new CatalogPageScanMessage
                {
                    ScanId = x.ScanId,
                    PageId = x.PageId,
                }).ToList());

                scan.ParsedState = CatalogIndexScanState.Enqueued;
                await _storageService.UpdateIndexScanAsync(scan);
            }

            // Enqueued -> Complete: check if all of the page scans are complete
            if (scan.ParsedState == CatalogIndexScanState.Enqueued)
            {
                var countLowerBound = await _storageService.GetPageScanCountLowerBoundAsync(scan.ScanId);
                if (countLowerBound > 0)
                {
                    _logger.LogInformation("There are at least {Count} page scans pending.", countLowerBound);

                    await _messageEnqueuer.EnqueueAsync(new[] { message }, TimeSpan.FromSeconds(5));
                }
                else
                {
                    _logger.LogInformation("The catalog scan is complete.");

                    scan.ParsedState = CatalogIndexScanState.Complete;
                    await _storageService.UpdateIndexScanAsync(scan);
                }
            }
        }

        private async Task<CatalogIndex> GetCatalogIndexAsync()
        {
            _logger.LogInformation("Loading catalog index.");
            var catalogIndex = await _catalogClient.GetCatalogIndexAsync();
            return catalogIndex;
        }

        private static List<CatalogPageScan> GetPageScans(CatalogIndexScan scan, CatalogIndex catalogIndex)
        {
            var pages = catalogIndex.GetPagesInBounds(scan.Min.Value, scan.Max.Value);
            var maxRowKeyLength = (pages.Count - 1).ToString().Length;

            var pageScans = pages
                .OrderBy(x => x.CommitTimestamp)
                .Select((x, index) => new CatalogPageScan(
                    scan.ScanId,
                    index.ToString(CultureInfo.InvariantCulture).PadLeft(maxRowKeyLength, '0'))
                {
                    ParsedScanType = scan.ParsedScanType,
                    Min = scan.Min.Value,
                    Max = scan.Max.Value,
                    Url = x.Url,
                })
                .ToList();
            return pageScans;
        }
    }
}

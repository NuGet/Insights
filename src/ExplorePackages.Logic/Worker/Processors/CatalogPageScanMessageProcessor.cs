using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class CatalogPageScanMessageProcessor : IMessageProcessor<CatalogPageScanMessage>
    {
        private readonly CatalogClient _catalogClient;
        private readonly MessageEnqueuer _messageEnqueuer;
        private readonly CatalogScanStorageService _catalogScanStorageService;
        private readonly LatestPackageLeafService _latestPackageLeafService;
        private readonly ILogger<CatalogPageScanMessageProcessor> _logger;

        public CatalogPageScanMessageProcessor(
            CatalogClient catalogClient,
            MessageEnqueuer messageEnqueuer,
            CatalogScanStorageService catalogScanStorageService,
            LatestPackageLeafService latestPackageLeafService,
            ILogger<CatalogPageScanMessageProcessor> logger)
        {
            _catalogClient = catalogClient;
            _messageEnqueuer = messageEnqueuer;
            _catalogScanStorageService = catalogScanStorageService;
            _latestPackageLeafService = latestPackageLeafService;
            _logger = logger;
        }

        public async Task ProcessAsync(CatalogPageScanMessage message)
        {
            var pageScan = await _catalogScanStorageService.GetPageScanAsync(message.ScanId, message.PageId);
            if (pageScan == null)
            {
                return;
            }    

            _logger.LogInformation("Loading catalog page URL: {Url}", pageScan.Url);
            var page = await _catalogClient.GetCatalogPageAsync(pageScan.Url);

            var leaves = page.GetLeavesInBounds(pageScan.Min, pageScan.Max, excludeRedundantLeaves: false);

            if (pageScan.ParsedScanType == CatalogScanType.DownloadPages)
            {
                // Do nothing more.
            }
            else if (pageScan.ParsedScanType == CatalogScanType.DownloadLeaves)
            {
                _logger.LogInformation("Starting scan of {LeafCount} leaves from ({Min:O}, {Max:O}].", leaves.Count, pageScan.Min, pageScan.Max);
                await _messageEnqueuer.EnqueueAsync(leaves
                    .Select(x => new CatalogLeafMessage { LeafType = x.Type, Url = x.Url, ScanType = pageScan.ParsedScanType })
                    .ToList());
            }
            else if (pageScan.ParsedScanType == CatalogScanType.FindLatestLeaves)
            {
                await _latestPackageLeafService.AddAsync(pageScan.ScanId, leaves);
            }

            await _catalogScanStorageService.DeletePageScanAsync(pageScan);
        }
    }
}

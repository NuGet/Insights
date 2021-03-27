using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogScanExpandService
    {
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly ILogger<CatalogScanExpandService> _logger;

        public CatalogScanExpandService(
            IMessageEnqueuer messageEnqueuer,
            ILogger<CatalogScanExpandService> logger)
        {
            _messageEnqueuer = messageEnqueuer;
            _logger = logger;
        }

        public async Task EnqueueLeafScansAsync(IReadOnlyList<CatalogLeafScan> leafScans)
        {
            _logger.LogInformation("Enqueuing a scan of {LeafCount} leaves.", leafScans.Count);
            await _messageEnqueuer.EnqueueAsync(leafScans
                .Select(x => new CatalogLeafScanMessage
                {
                    StorageSuffix = x.StorageSuffix,
                    ScanId = x.ScanId,
                    PageId = x.PageId,
                    LeafId = x.GetLeafId(),
                })
                .ToList());
        }
    }
}

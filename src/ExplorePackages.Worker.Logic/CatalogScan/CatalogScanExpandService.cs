using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogScanExpandService
    {
        private readonly MessageEnqueuer _messageEnqueuer;
        private readonly CatalogScanStorageService _storageService;
        private readonly ILogger<CatalogScanExpandService> _logger;

        public CatalogScanExpandService(
            MessageEnqueuer messageEnqueuer,
            CatalogScanStorageService storageService,
            ILogger<CatalogScanExpandService> logger)
        {
            _messageEnqueuer = messageEnqueuer;
            _storageService = storageService;
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
                    LeafId = x.LeafId,
                })
                .ToList());
        }
    }
}

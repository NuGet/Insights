using System;
using System.Collections.Generic;
using System.Globalization;
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

        public async Task InsertLeafScansAsync(string storageSuffix, string scanId, string pageId, IReadOnlyList<CatalogLeafScan> leafScans)
        {
            var createdLeaves = await _storageService.GetLeafScansAsync(storageSuffix, scanId, pageId);

            var allUrls = leafScans.Select(x => x.Url).ToHashSet();
            var createdUrls = createdLeaves.Select(x => x.Url).ToHashSet();
            var uncreatedUrls = allUrls.Except(createdUrls).ToHashSet();

            if (createdUrls.Except(allUrls).Any())
            {
                throw new InvalidOperationException("There should not be any extra leaf scan entities.");
            }

            var uncreatedLeafScans = leafScans
                .Where(x => uncreatedUrls.Contains(x.Url))
                .ToList();
            await _storageService.InsertAsync(uncreatedLeafScans);
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

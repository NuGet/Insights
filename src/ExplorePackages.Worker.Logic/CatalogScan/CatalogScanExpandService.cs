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

        public CatalogPageScan CreatePageScan(CatalogIndexScan scan, string url, int rank)
        {
            return new CatalogPageScan(
                scan.StorageSuffix,
                scan.ScanId,
                "P" + rank.ToString(CultureInfo.InvariantCulture).PadLeft(10, '0'))
            {
                ParsedDriverType = scan.ParsedDriverType,
                DriverParameters = scan.DriverParameters,
                ParsedState = CatalogScanState.Created,
                Min = scan.Min.Value,
                Max = scan.Max.Value,
                Url = url,
                Rank = rank,
            };
        }

        public List<CatalogLeafScan> CreateLeafScans(CatalogPageScan scan, List<CatalogLeafItem> items, Dictionary<CatalogLeafItem, int> leafItemToRank)
        {
            return items
                .OrderBy(x => leafItemToRank[x])
                .Select(x => new CatalogLeafScan(
                    scan.StorageSuffix,
                    scan.ScanId,
                    scan.PageId,
                    "L" + leafItemToRank[x].ToString(CultureInfo.InvariantCulture).PadLeft(10, '0'))
                {
                    ParsedDriverType = scan.ParsedDriverType,
                    DriverParameters = scan.DriverParameters,
                    Url = x.Url,
                    ParsedLeafType = x.Type,
                    CommitId = x.CommitId,
                    CommitTimestamp = x.CommitTimestamp,
                    PackageId = x.PackageId,
                    PackageVersion = x.PackageVersion,
                    Rank = leafItemToRank[x],
                })
                .ToList();
        }

        public async Task InsertLeafScansAsync(CatalogPageScan scan, IReadOnlyList<CatalogLeafScan> leafScans)
        {
            var createdLeaves = await _storageService.GetLeafScansAsync(scan.StorageSuffix, scan.ScanId, scan.PageId);

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

using System;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Worker.FindPackageAssets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogScanService
    {
        private readonly CatalogClient _catalogClient;
        private readonly CursorStorageService _cursorStorageService;
        private readonly MessageEnqueuer _messageEnqueuer;
        private readonly SchemaSerializer _serializer;
        private readonly CatalogScanStorageService _catalogScanStorageService;
        private readonly AutoRenewingStorageLeaseService _leaseService;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;
        private readonly ILogger<CatalogScanService> _logger;

        public CatalogScanService(
            CatalogClient catalogClient,
            CursorStorageService cursorStorageService,
            MessageEnqueuer messageEnqueuer,
            SchemaSerializer serializer,
            CatalogScanStorageService catalogScanStorageService,
            AutoRenewingStorageLeaseService leaseService,
            IOptions<ExplorePackagesWorkerSettings> options,
            ILogger<CatalogScanService> logger)
        {
            _catalogClient = catalogClient;
            _cursorStorageService = cursorStorageService;
            _messageEnqueuer = messageEnqueuer;
            _serializer = serializer;
            _catalogScanStorageService = catalogScanStorageService;
            _leaseService = leaseService;
            _options = options;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await _cursorStorageService.InitializeAsync();
            await _catalogScanStorageService.InitializeAsync();
            await _messageEnqueuer.InitializeAsync();
            await _leaseService.InitializeAsync();
        }

        public async Task RequeueAsync(string cursorName, string scanId)
        {
            var indexScan = await _catalogScanStorageService.GetIndexScanAsync(cursorName, scanId);
            if (indexScan.ParsedState != CatalogScanState.Waiting)
            {
                return;
            }

            var pageScans = await _catalogScanStorageService.GetPageScansAsync(indexScan.StorageSuffix, indexScan.ScanId);
            foreach (var pageScan in pageScans)
            {
                var leafScans = await _catalogScanStorageService.GetLeafScansAsync(pageScan.StorageSuffix, pageScan.ScanId, pageScan.PageId);
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

            await _messageEnqueuer.EnqueueAsync(pageScans
                .Select(x => new CatalogPageScanMessage
                {
                    StorageSuffix = x.StorageSuffix,
                    ScanId = x.ScanId,
                    PageId = x.PageId,
                })
                .ToList());

            await _messageEnqueuer.EnqueueAsync(new[]
            {
                new CatalogIndexScanMessage
                {
                    CursorName = indexScan.CursorName,
                    ScanId = indexScan.ScanId,
                },
            });
        }

        public async Task<CursorTableEntity> GetCursorAsync(CatalogScanType type)
        {
            return await _cursorStorageService.GetOrCreateAsync($"CatalogScan-{type}");
        }

        public async Task<CatalogIndexScan> UpdateFindPackageAssetsAsync() => await UpdateFindPackageAssetsAsync(max: null);
        public async Task<CatalogIndexScan> UpdateFindPackageAssetsAsync(DateTimeOffset max) => await UpdateFindPackageAssetsAsync((DateTimeOffset?)max);

        private async Task<CatalogIndexScan> UpdateFindPackageAssetsAsync(DateTimeOffset? max)
        {
            return await UpdateAsync(
                CatalogScanType.FindPackageAssets,
                _serializer.Serialize(new CatalogLeafToCsvParameters
                {
                    BucketCount = _options.Value.AppendResultStorageBucketCount,
                }).AsString(),
                max);
        }

        private async Task<CatalogIndexScan> UpdateAsync(CatalogScanType type, string parameters, DateTimeOffset? max)
        {
            // Check if a scan is already running, outside the lease.
            var cursor = await GetCursorAsync(type);
            var incompleteScan = await GetLatestIncompleteScanAsync(cursor.Name);
            if (incompleteScan != null)
            {
                return incompleteScan;
            }

            // Determine the bounds of the scan.
            var index = await _catalogClient.GetCatalogIndexAsync();
            var min = new[] { cursor.Value, CatalogClient.NuGetOrgMin }.Max();
            max = max.GetValueOrDefault(index.CommitTimestamp);

            // var testMax = DateTimeOffset.Parse("2019-02-20T20:18:34.697022Z");
            // var testMax = DateTimeOffset.Parse("2018-09-20T01:46:19.1755275Z");
            /*
            if (max > testMax)
            {
                max = testMax;
            }
            */

            if (min >= max)
            {
                return null;
            }

            await using (var lease = await _leaseService.TryAcquireAsync($"Start-{cursor.Name}"))
            {
                if (!lease.Acquired)
                {
                    return null;
                }

                // Check if a scan is already running, inside the lease.
                incompleteScan = await GetLatestIncompleteScanAsync(cursor.Name);
                if (incompleteScan != null)
                {
                    return incompleteScan;
                }

                // Start a new scan.
                _logger.LogInformation("Attempting to start a catalog index scan from ({Min}, {Max}].", min, max);
                var scanId = StorageUtility.GenerateDescendingId();
                var catalogIndexScanMessage = new CatalogIndexScanMessage
                {
                    CursorName = cursor.Name,
                    ScanId = scanId.ToString(),
                };
                await _messageEnqueuer.EnqueueAsync(new[] { catalogIndexScanMessage });

                var catalogIndexScan = new CatalogIndexScan(cursor.Name, scanId.ToString(), scanId.Unique)
                {
                    ParsedScanType = type,
                    ScanParameters = parameters,
                    ParsedState = CatalogScanState.Created,
                    Min = min,
                    Max = max.Value,
                };
                await _catalogScanStorageService.InitializeChildTablesAsync(catalogIndexScan.StorageSuffix);
                await _catalogScanStorageService.InsertAsync(catalogIndexScan);

                return catalogIndexScan;
            }
        }

        private async Task<CatalogIndexScan> GetLatestIncompleteScanAsync(string cursorName)
        {
            var latestScans = await _catalogScanStorageService.GetLatestIndexScans(cursorName);
            var incompleteScans = latestScans.Where(x => x.ParsedState != CatalogScanState.Complete);
            if (incompleteScans.Any())
            {
                return incompleteScans.First();
            }

            return null;
        }
    }
}

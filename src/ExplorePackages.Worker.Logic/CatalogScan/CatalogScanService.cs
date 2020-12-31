using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Worker.FindLatestLeaves;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogScanService
    {
        private const string NoParameters = "";

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

        public async Task<CursorTableEntity> GetCursorAsync(CatalogScanType type)
        {
            return await _cursorStorageService.GetOrCreateAsync(GetCursorName(type));
        }

        public async Task RequeueAsync(CatalogScanType type, string scanId)
        {
            var cursorName = GetCursorName(type);
            var indexScan = await _catalogScanStorageService.GetIndexScanAsync(cursorName, scanId);
            if (indexScan.ParsedState != CatalogScanState.Waiting)
            {
                return;
            }

            var pageScans = await _catalogScanStorageService.GetPageScansAsync(indexScan.StorageSuffix, indexScan.ScanId);
            var leafScans = new List<CatalogLeafScan>();
            foreach (var pageScan in pageScans)
            {
                var pageLeafScans = await _catalogScanStorageService.GetLeafScansAsync(pageScan.StorageSuffix, pageScan.ScanId, pageScan.PageId);
                leafScans.AddRange(pageLeafScans);
            }

            await _messageEnqueuer.EnqueueAsync(leafScans
                .Select(x => new CatalogLeafScanMessage
                {
                    StorageSuffix = x.StorageSuffix,
                    ScanId = x.ScanId,
                    PageId = x.PageId,
                    LeafId = x.LeafId,
                })
                .ToList());

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

        public string GetCursorName(CatalogScanType type)
        {
            return $"CatalogScan-{type}";
        }

        public async Task<CatalogIndexScan> UpdateAsync(CatalogScanType type, DateTimeOffset? max)
        {
            switch (type)
            {
                case CatalogScanType.FindCatalogLeafItems:
                    return await UpdateAsync(type, NoParameters, DateTimeOffset.MinValue, max);
                case CatalogScanType.FindLatestLeaves:
                    return await UpdateFindLatestLeavesAsync(max);
                case CatalogScanType.FindPackageAssemblies:
                case CatalogScanType.FindPackageAssets:
                    return await UpdateCatalogLeafToCsvAsync(type, max);
                default:
                    throw new NotSupportedException();
            }
        }

        public async Task<CatalogIndexScan> StartSpecificFindLatestLeavesAsync(
            string scanId,
            string storageSuffix,
            string prefix,
            string tableName,
            DateTimeOffset min,
            DateTimeOffset? max)
        {
            var parameters = new FindLatestLeavesParameters
            {
                Prefix = prefix,
                TableName = tableName,
            };

            return await StartCursorlessAsync(
                scanId,
                storageSuffix,
                CatalogScanType.FindLatestLeaves,
                parameters: _serializer.Serialize(parameters).AsString(),
                min,
                max);
        }

        private async Task<CatalogIndexScan> UpdateFindLatestLeavesAsync(DateTimeOffset? max)
        {
            var parameters = new FindLatestLeavesParameters
            {
                Prefix = string.Empty,
                TableName = _options.Value.LatestLeavesTableName,
            };

            return await UpdateAsync(
                CatalogScanType.FindLatestLeaves,
                parameters: _serializer.Serialize(parameters).AsString(),
                min: DateTimeOffset.MinValue,
                max);
        }

        private async Task<CatalogIndexScan> UpdateCatalogLeafToCsvAsync(CatalogScanType catalogScanType, DateTimeOffset? max)
        {
            var parameters = new CatalogLeafToCsvParameters
            {
                BucketCount = _options.Value.AppendResultStorageBucketCount,
            };

            return await UpdateAsync(
                catalogScanType,
                parameters: _serializer.Serialize(parameters).AsString(),
                min: CatalogClient.NuGetOrgMin,
                max);
        }

        private async Task<CatalogIndexScan> UpdateAsync(CatalogScanType type, string parameters, DateTimeOffset min, DateTimeOffset? max)
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
            min = new[] { cursor.Value, min }.Max();
            max = max.GetValueOrDefault(index.CommitTimestamp);

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

                var descendingId = StorageUtility.GenerateDescendingId();
                return await StartWithoutLeaseAsync(
                    cursor.Name,
                    descendingId.ToString(),
                    descendingId.Unique,
                    type,
                    parameters,
                    min,
                    max);
            }
        }

        private async Task<CatalogIndexScan> StartCursorlessAsync(
            string scanId,
            string storageSuffix,
            CatalogScanType type,
            string parameters,
            DateTimeOffset min,
            DateTimeOffset? max)
        {
            var cursorName = string.Empty;

            // Check if a scan is already running, outside the lease.
            var scan = await _catalogScanStorageService.GetIndexScanAsync(cursorName, scanId);
            if (scan != null)
            {
                return scan;
            }

            // Use a rather generic lease, to simplify clean-up.
            await using (var lease = await _leaseService.TryAcquireAsync($"Start-{GetCursorName(type)}"))
            {
                if (!lease.Acquired)
                {
                    return null;
                }

                // Check if a scan is already running, inside the lease.
                scan = await _catalogScanStorageService.GetIndexScanAsync(cursorName, scanId);
                if (scan != null)
                {
                    return scan;
                }

                return await StartWithoutLeaseAsync(
                    cursorName,
                    scanId,
                    storageSuffix,
                    type,
                    parameters,
                    min,
                    max);
            }
        }

        private async Task<CatalogIndexScan> StartWithoutLeaseAsync(
            string cursorName,
            string scanId,
            string storageSuffix,
            CatalogScanType type,
            string parameters,
            DateTimeOffset min,
            DateTimeOffset? max)
        {
            // Start a new scan.
            _logger.LogInformation("Attempting to start a {Type} catalog index scan from ({Min}, {Max}].", type, min, max);

            var catalogIndexScanMessage = new CatalogIndexScanMessage
            {
                CursorName = cursorName,
                ScanId = scanId,
            };
            await _messageEnqueuer.EnqueueAsync(new[] { catalogIndexScanMessage });
            var catalogIndexScan = new CatalogIndexScan(cursorName, scanId, storageSuffix)
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

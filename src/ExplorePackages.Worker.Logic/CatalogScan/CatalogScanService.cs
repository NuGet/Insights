using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Worker.FindLatestPackageLeaf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogScanService
    {
        private const string NoParameters = "";

        private readonly CatalogClient _catalogClient;
        private readonly CursorStorageService _cursorStorageService;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly SchemaSerializer _serializer;
        private readonly CatalogScanStorageService _catalogScanStorageService;
        private readonly AutoRenewingStorageLeaseService _leaseService;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;
        private readonly ILogger<CatalogScanService> _logger;

        public CatalogScanService(
            CatalogClient catalogClient,
            CursorStorageService cursorStorageService,
            IMessageEnqueuer messageEnqueuer,
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

        public async Task<CursorTableEntity> GetCursorAsync(CatalogScanDriverType driverType)
        {
            return await _cursorStorageService.GetOrCreateAsync(GetCursorName(driverType));
        }

        public async Task RequeueAsync(CatalogScanDriverType driverType, string scanId)
        {
            var cursorName = GetCursorName(driverType);
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

        public string GetCursorName(CatalogScanDriverType driverType)
        {
            return $"CatalogScan-{driverType}";
        }

        public async Task<CatalogIndexScan> UpdateAsync(CatalogScanDriverType driverType, DateTimeOffset? max, bool? onlyLatestLeaves)
        {
            switch (driverType)
            {
                case CatalogScanDriverType.FindCatalogLeafItem:
                    if (onlyLatestLeaves.HasValue && onlyLatestLeaves.Value)
                    {
                        throw new NotSupportedException("When finding catalog leaf items all leaves will be reported, not just the latest.");
                    }
                    return await UpdateAsync(driverType, NoParameters, DateTimeOffset.MinValue, max);
                case CatalogScanDriverType.FindLatestPackageLeaf:
                    if (onlyLatestLeaves.HasValue && !onlyLatestLeaves.Value)
                    {
                        throw new NotSupportedException("When finding latest leaves, only the latest leaves will be reported.");
                    }
                    return await UpdateFindLatestLeafAsync(max);
                case CatalogScanDriverType.FindPackageAssembly:
                    return await UpdateFindPackageAssemblyAsync(onlyLatestLeaves.GetValueOrDefault(true), max);
                case CatalogScanDriverType.FindPackageAsset:
                    return await UpdateFindPackageAssetAsync(onlyLatestLeaves.GetValueOrDefault(true), max);
                case CatalogScanDriverType.FindPackageFile:
                    if (onlyLatestLeaves.HasValue && !onlyLatestLeaves.Value)
                    {
                        throw new NotSupportedException("When finding package files, only the latest leaves will be reported.");
                    }
                    return await UpdateFindPackageFilesAsync(max);
                default:
                    throw new NotSupportedException();
            }
        }

        public async Task<CatalogIndexScan> GetOrStartFindLatestCatalogLeafScanAsync(
            string scanId,
            string storageSuffix,
            CatalogIndexScanMessage parentScanMessage,
            DateTimeOffset min,
            DateTimeOffset? max)
        {
            return await GetOrStartCursorlessAsync(
                scanId,
                storageSuffix,
                CatalogScanDriverType.FindLatestCatalogLeafScan,
                parameters: _serializer.Serialize(parentScanMessage).AsString(),
                min,
                max);
        }

        private async Task<CatalogIndexScan> UpdateFindLatestLeafAsync(DateTimeOffset? max)
        {
            var parameters = new LatestPackageLeafParameters
            {
                Prefix = string.Empty,
                StorageSuffix = string.Empty,
            };

            return await UpdateAsync(
                CatalogScanDriverType.FindLatestPackageLeaf,
                parameters: _serializer.Serialize(parameters).AsString(),
                min: DateTimeOffset.MinValue,
                max);
        }

        public async Task<CatalogIndexScan> UpdateFindPackageAssemblyAsync(bool onlyLatestLeaves, DateTimeOffset? max)
        {
            return await UpdateCatalogLeafToCsvAsync(CatalogScanDriverType.FindPackageAssembly, onlyLatestLeaves, max);
        }

        public async Task<CatalogIndexScan> UpdateFindPackageAssetAsync(bool onlyLatestLeaves, DateTimeOffset? max)
        {
            return await UpdateCatalogLeafToCsvAsync(CatalogScanDriverType.FindPackageAsset, onlyLatestLeaves, max);
        }

        private async Task<CatalogIndexScan> UpdateCatalogLeafToCsvAsync(CatalogScanDriverType driverType, bool onlyLatestLeaves, DateTimeOffset? max)
        {
            var parameters = new CatalogLeafToCsvParameters
            {
                BucketCount = _options.Value.AppendResultStorageBucketCount,
                OnlyLatestLeaves = onlyLatestLeaves,
            };

            return await UpdateAsync(
                driverType,
                parameters: _serializer.Serialize(parameters).AsString(),
                min: onlyLatestLeaves ? DateTimeOffset.MinValue : CatalogClient.NuGetOrgMin,
                max);
        }

        private async Task<CatalogIndexScan> UpdateFindPackageFilesAsync(DateTimeOffset? max)
        {
            return await UpdateAsync(
                CatalogScanDriverType.FindPackageFile,
                parameters: null,
                min: DateTimeOffset.MinValue,
                max);
        }

        private async Task<CatalogIndexScan> UpdateAsync(CatalogScanDriverType driverType, string parameters, DateTimeOffset min, DateTimeOffset? max)
        {
            // Check if a scan is already running, outside the lease.
            var cursor = await GetCursorAsync(driverType);
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
                    driverType,
                    parameters,
                    min,
                    max);
            }
        }

        private async Task<CatalogIndexScan> GetOrStartCursorlessAsync(
            string scanId,
            string storageSuffix,
            CatalogScanDriverType driverType,
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
            await using (var lease = await _leaseService.TryAcquireAsync($"Start-{GetCursorName(driverType)}"))
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
                    driverType,
                    parameters,
                    min,
                    max);
            }
        }

        private async Task<CatalogIndexScan> StartWithoutLeaseAsync(
            string cursorName,
            string scanId,
            string storageSuffix,
            CatalogScanDriverType driverType,
            string parameters,
            DateTimeOffset min,
            DateTimeOffset? max)
        {
            // Start a new scan.
            _logger.LogInformation("Attempting to start a {DriverType} catalog index scan from ({Min}, {Max}].", driverType, min, max);

            var catalogIndexScanMessage = new CatalogIndexScanMessage
            {
                CursorName = cursorName,
                ScanId = scanId,
            };
            await _messageEnqueuer.EnqueueAsync(new[] { catalogIndexScanMessage });
            var catalogIndexScan = new CatalogIndexScan(cursorName, scanId, storageSuffix)
            {
                ParsedDriverType = driverType,
                DriverParameters = parameters,
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

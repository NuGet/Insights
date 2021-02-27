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
        private readonly CursorStorageService _cursorStorageService;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly SchemaSerializer _serializer;
        private readonly CatalogScanStorageService _catalogScanStorageService;
        private readonly AutoRenewingStorageLeaseService _leaseService;
        private readonly IRemoteCursorClient _remoteCursorClient;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;
        private readonly ILogger<CatalogScanService> _logger;

        public CatalogScanService(
            CursorStorageService cursorStorageService,
            IMessageEnqueuer messageEnqueuer,
            SchemaSerializer serializer,
            CatalogScanStorageService catalogScanStorageService,
            AutoRenewingStorageLeaseService leaseService,
            IRemoteCursorClient remoteCursorClient,
            IOptions<ExplorePackagesWorkerSettings> options,
            ILogger<CatalogScanService> logger)
        {
            _cursorStorageService = cursorStorageService;
            _messageEnqueuer = messageEnqueuer;
            _serializer = serializer;
            _catalogScanStorageService = catalogScanStorageService;
            _leaseService = leaseService;
            _remoteCursorClient = remoteCursorClient;
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

        public async Task<DateTimeOffset> GetCursorValueAsync(CatalogScanDriverType driverType)
        {
            var entity = await GetCursorAsync(driverType);
            return entity.Value;
        }

        public async Task RequeueAsync(CatalogScanDriverType driverType, string scanId)
        {
            var cursorName = GetCursorName(driverType);
            var indexScan = await _catalogScanStorageService.GetIndexScanAsync(cursorName, scanId);
            if (indexScan.ParsedState != CatalogIndexScanState.Working)
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

        public async Task<CatalogScanServiceResult> UpdateAsync(CatalogScanDriverType driverType, DateTimeOffset? max, bool? onlyLatestLeaves)
        {
            switch (driverType)
            {
                case CatalogScanDriverType.CatalogLeafItemToCsv:
                    if (onlyLatestLeaves.HasValue && onlyLatestLeaves.Value)
                    {
                        throw new NotSupportedException("When finding catalog leaf items all leaves will be reported, not just the latest.");
                    }
                    return await UpdateAsync(
                        driverType,
                        parameters: null,
                        CatalogClient.NuGetOrgMin,
                        max);
                case CatalogScanDriverType.FindLatestPackageLeaf:
                    if (onlyLatestLeaves.HasValue && !onlyLatestLeaves.Value)
                    {
                        throw new NotSupportedException("When finding latest leaves, only the latest leaves will be reported.");
                    }
                    return await UpdateFindLatestLeafAsync(max);
                case CatalogScanDriverType.PackageArchiveEntryToCsv:
                case CatalogScanDriverType.PackageAssemblyToCsv:
                case CatalogScanDriverType.PackageAssetToCsv:
                case CatalogScanDriverType.PackageSignatureToCsv:
                case CatalogScanDriverType.PackageManifestToCsv:
                    return await UpdateCatalogLeafToCsvAsync(driverType, onlyLatestLeaves.GetValueOrDefault(true), max);
                case CatalogScanDriverType.LoadPackageArchive:
                case CatalogScanDriverType.LoadPackageManifest:
                case CatalogScanDriverType.LoadPackageVersion:
                    if (onlyLatestLeaves.HasValue && !onlyLatestLeaves.Value)
                    {
                        throw new NotSupportedException("For catalog scan drivers that don't support parameters, only the latest leaves will be reported.");
                    }
                    return await UpdateParameterlessAsync(driverType, max);
                default:
                    throw new NotSupportedException();
            }
        }

        public async Task<CatalogIndexScan> GetOrStartFindLatestCatalogLeafScanAsync(
            string scanId,
            string storageSuffix,
            CatalogIndexScanMessage parentScanMessage,
            DateTimeOffset min,
            DateTimeOffset max)
        {
            return await GetOrStartCursorlessAsync(
                scanId,
                storageSuffix,
                CatalogScanDriverType.Internal_FindLatestCatalogLeafScan,
                parameters: _serializer.Serialize(parentScanMessage).AsString(),
                min,
                max);
        }

        private async Task<CatalogScanServiceResult> UpdateFindLatestLeafAsync(DateTimeOffset? max)
        {
            var parameters = new LatestPackageLeafParameters
            {
                Prefix = string.Empty,
                StorageSuffix = string.Empty,
            };

            return await UpdateAsync(
                CatalogScanDriverType.FindLatestPackageLeaf,
                parameters: _serializer.Serialize(parameters).AsString(),
                min: CatalogClient.NuGetOrgMinDeleted,
                max);
        }

        private async Task<CatalogScanServiceResult> UpdateCatalogLeafToCsvAsync(CatalogScanDriverType driverType, bool onlyLatestLeaves, DateTimeOffset? max)
        {
            var parameters = new CatalogLeafToCsvParameters
            {
                BucketCount = _options.Value.AppendResultStorageBucketCount,
                OnlyLatestLeaves = onlyLatestLeaves,
            };

            return await UpdateAsync(
                driverType,
                parameters: _serializer.Serialize(parameters).AsString(),
                min: onlyLatestLeaves ? CatalogClient.NuGetOrgMinDeleted : CatalogClient.NuGetOrgMinAvailable,
                max);
        }

        private async Task<CatalogScanServiceResult> UpdateParameterlessAsync(CatalogScanDriverType driverType, DateTimeOffset? max)
        {
            return await UpdateAsync(
                driverType,
                parameters: null,
                min: CatalogClient.NuGetOrgMinDeleted,
                max);
        }

        private async Task<CatalogScanServiceResult> UpdateAsync(CatalogScanDriverType driverType, string parameters, DateTimeOffset min, DateTimeOffset? max)
        {
            // Check if a scan is already running, outside the lease.
            var cursor = await GetCursorAsync(driverType);
            var incompleteScan = await GetLatestIncompleteScanAsync(cursor.Name);
            if (incompleteScan != null)
            {
                return new CatalogScanServiceResult(CatalogScanServiceResultType.AlreadyRunning, dependencyName: null, incompleteScan);
            }

            if (cursor.Value > CursorTableEntity.Min)
            {
                // Use the cursor value as the min if it's greater than the provided min. We don't want to process leaves
                // that have already been scanned.
                min = cursor.Value;
            }

            (var dependencyName, var dependencyMax) = await GetDependencyMaxAsync(driverType);

            if (dependencyMax <= CursorTableEntity.Min)
            {
                return new CatalogScanServiceResult(CatalogScanServiceResultType.BlockedByDependency, dependencyName, scan: null);
            }

            var tookDependencyMax = false;
            if (!max.HasValue)
            {
                max = dependencyMax;
                tookDependencyMax = true;
            }
            else
            {
                if (max > dependencyMax)
                {
                    return new CatalogScanServiceResult(CatalogScanServiceResultType.BlockedByDependency, dependencyName, scan: null);
                }
            }

            if (max < min)
            {
                // If the provided max is less than the smart min, revert the min to the absolute min. This allows
                // very short test runs from the beginning of the catalog.
                min = CursorTableEntity.Min;
            }

            if (min > max)
            {
                return new CatalogScanServiceResult(CatalogScanServiceResultType.MinAfterMax, dependencyName: null, scan: null);
            }

            if (!tookDependencyMax && min == max)
            {
                return new CatalogScanServiceResult(CatalogScanServiceResultType.FullyCaughtUpWithMax, dependencyName: null, scan: null);
            }

            if (min == dependencyMax)
            {
                return new CatalogScanServiceResult(CatalogScanServiceResultType.FullyCaughtUpWithDependency, dependencyName, scan: null);
            }

            await using (var lease = await _leaseService.TryAcquireAsync($"Start-{cursor.Name}"))
            {
                if (!lease.Acquired)
                {
                    return new CatalogScanServiceResult(CatalogScanServiceResultType.UnavailableLease, dependencyName: null, scan: null);
                }

                // Check if a scan is already running, inside the lease.
                incompleteScan = await GetLatestIncompleteScanAsync(cursor.Name);
                if (incompleteScan != null)
                {
                    return new CatalogScanServiceResult(CatalogScanServiceResultType.AlreadyRunning, dependencyName: null, incompleteScan);
                }

                var descendingId = StorageUtility.GenerateDescendingId();
                var newScan = await StartWithoutLeaseAsync(
                    cursor.Name,
                    descendingId.ToString(),
                    descendingId.Unique,
                    driverType,
                    parameters,
                    min,
                    max.Value);

                return new CatalogScanServiceResult(CatalogScanServiceResultType.NewStarted, dependencyName: null, newScan);
            }
        }

        private async Task<CatalogIndexScan> GetOrStartCursorlessAsync(
            string scanId,
            string storageSuffix,
            CatalogScanDriverType driverType,
            string parameters,
            DateTimeOffset min,
            DateTimeOffset max)
        {
            var cursorName = string.Empty;

            // Check if a scan is already running, outside the lease.
            var incompleteScan = await _catalogScanStorageService.GetIndexScanAsync(cursorName, scanId);
            if (incompleteScan != null)
            {
                return incompleteScan;
            }

            // Use a rather generic lease, to simplify clean-up.
            await using (var lease = await _leaseService.TryAcquireAsync($"Start-{GetCursorName(driverType)}"))
            {
                if (!lease.Acquired)
                {
                    throw new InvalidOperationException("Another thread is already starting the scan.");
                }

                // Check if a scan is already running, inside the lease.
                incompleteScan = await _catalogScanStorageService.GetIndexScanAsync(cursorName, scanId);
                if (incompleteScan != null)
                {
                    return incompleteScan;
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
            DateTimeOffset max)
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
                ParsedState = CatalogIndexScanState.Created,
                Min = min,
                Max = max,
            };
            await _catalogScanStorageService.InsertAsync(catalogIndexScan);

            return catalogIndexScan;
        }

        private async Task<CatalogIndexScan> GetLatestIncompleteScanAsync(string cursorName)
        {
            var latestScans = await _catalogScanStorageService.GetLatestIndexScans(cursorName);
            var incompleteScans = latestScans.Where(x => x.ParsedState != CatalogIndexScanState.Complete);
            if (incompleteScans.Any())
            {
                return incompleteScans.First();
            }

            return null;
        }

        private static readonly (string Name, Func<CatalogScanService, Task<DateTimeOffset>> GetValueAsync)[] LoadPackageArchive = new (string Name, Func<CatalogScanService, Task<DateTimeOffset>> GetValueAsync)[]
        {
            (CatalogScanDriverType.LoadPackageArchive.ToString(), x => x.GetCursorValueAsync(CatalogScanDriverType.LoadPackageArchive)),
        };

        private static readonly (string Name, Func<CatalogScanService, Task<DateTimeOffset>> GetValueAsync)[] LoadPackageManifest = new (string Name, Func<CatalogScanService, Task<DateTimeOffset>> GetValueAsync)[]
        {
            (CatalogScanDriverType.LoadPackageManifest.ToString(), x => x.GetCursorValueAsync(CatalogScanDriverType.LoadPackageManifest)),
        };

        private static readonly (string Name, Func<CatalogScanService, Task<DateTimeOffset>> GetValueAsync)[] Catalog = new (string Name, Func<CatalogScanService, Task<DateTimeOffset>> GetValueAsync)[]
        {
            ("NuGet.org catalog", x => x._remoteCursorClient.GetCatalogAsync()),
        };

        private static readonly (string Name, Func<CatalogScanService, Task<DateTimeOffset>> GetValueAsync)[] FlatContainer = new (string Name, Func<CatalogScanService, Task<DateTimeOffset>> GetValueAsync)[]
        {
            ("NuGet.org flat container", x => x._remoteCursorClient.GetFlatContainerAsync()),
        };

        private static readonly IReadOnlyDictionary<CatalogScanDriverType, IReadOnlyList<(string Name, Func<CatalogScanService, Task<DateTimeOffset>> GetValueAsync)>> Dependencies = new Dictionary<CatalogScanDriverType, (string Name, Func<CatalogScanService, Task<DateTimeOffset>> GetValueAsync)[]>
        {
            { CatalogScanDriverType.CatalogLeafItemToCsv, Catalog },
            { CatalogScanDriverType.FindLatestPackageLeaf, Catalog },
            { CatalogScanDriverType.LoadPackageArchive, FlatContainer },
            { CatalogScanDriverType.LoadPackageManifest, FlatContainer },
            { CatalogScanDriverType.LoadPackageVersion, Catalog },
            { CatalogScanDriverType.PackageArchiveEntryToCsv, LoadPackageArchive },
            { CatalogScanDriverType.PackageAssetToCsv, LoadPackageArchive },
            { CatalogScanDriverType.PackageAssemblyToCsv, LoadPackageArchive },
            { CatalogScanDriverType.PackageSignatureToCsv, LoadPackageArchive },
            { CatalogScanDriverType.PackageManifestToCsv, LoadPackageManifest },
        }.ToDictionary(x => x.Key, x => (IReadOnlyList<(string Name, Func<CatalogScanService, Task<DateTimeOffset>> GetValueAsync)>)x.Value.ToList());

        private async Task<(string Name, DateTimeOffset Value)> GetDependencyMaxAsync(CatalogScanDriverType driverType)
        {
            string dependencyName = null;
            var max = DateTimeOffset.MaxValue;

            if (!Dependencies.TryGetValue(driverType, out var getCursors))
            {
                throw new InvalidOperationException($"No dependencies are defined for catalog scan driver {driverType}.");
            }

            foreach ((var name, var getCursor) in getCursors)
            {
                var dependencyMax = await getCursor(this);
                if (max > dependencyMax)
                {
                    dependencyName = name;
                    max = dependencyMax;
                }
            }

            return (dependencyName, max);
        }
    }
}

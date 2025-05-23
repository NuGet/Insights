// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using NuGet.Insights.Worker.LoadBucketedPackage;

namespace NuGet.Insights.Worker
{
    public class CatalogScanService
    {
        internal static readonly string NoCursor = string.Empty;

        private readonly CatalogScanCursorService _cursorService;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly CatalogScanStorageService _storageService;
        private readonly AutoRenewingStorageLeaseService _leaseService;
        private readonly TaskStateStorageService _taskStateStorageService;
        private readonly ICatalogScanDriverFactory _driverFactory;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ITelemetryClient _telemetryClient;
        private readonly ILogger<CatalogScanService> _logger;

        public CatalogScanService(
            CatalogScanCursorService cursorService,
            IMessageEnqueuer messageEnqueuer,
            CatalogScanStorageService catalogScanStorageService,
            AutoRenewingStorageLeaseService leaseService,
            TaskStateStorageService taskStateStorageService,
            ICatalogScanDriverFactory driverFactory,
            IOptions<NuGetInsightsWorkerSettings> options,
            ITelemetryClient telemetryClient,
            ILogger<CatalogScanService> logger)
        {
            _cursorService = cursorService;
            _messageEnqueuer = messageEnqueuer;
            _storageService = catalogScanStorageService;
            _leaseService = leaseService;
            _taskStateStorageService = taskStateStorageService;
            _driverFactory = driverFactory;
            _options = options;
            _telemetryClient = telemetryClient;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await Task.WhenAll(
                _cursorService.InitializeAsync(),
                _storageService.InitializeAsync(),
                _messageEnqueuer.InitializeAsync(),
                _leaseService.InitializeAsync());
        }

        public bool IsEnabled(CatalogScanDriverType type)
        {
            return _options.Value.DisabledDrivers == null || !_options.Value.DisabledDrivers.Contains(type);
        }

        public async Task<List<CatalogIndexScan>> AbortAllAsync()
        {
            var scans = new List<CatalogIndexScan>();
            foreach (var driverType in CatalogScanDriverMetadata.StartableDriverTypes)
            {
                var scan = await AbortAsync(driverType);
                if (scan is not null)
                {
                    scans.Add(scan);
                }
            }

            return scans;
        }

        public async Task DestroyAllOutputAsync()
        {
            await Task.WhenAll(CatalogScanDriverMetadata.StartableDriverTypes.Select(DestroyOutputAsync));
        }

        public async Task<CatalogIndexScan> AbortAsync(CatalogScanDriverType driverType)
        {
            var scan = await GetLatestIncompleteScanAsync(driverType);
            if (scan is null)
            {
                return null;
            }

            await AbortAsync(scan, delete: false);

            return scan;
        }

        public async Task<CatalogIndexScan> AbortAsync(CatalogScanDriverType driverType, string scanId)
        {
            var scan = await _storageService.GetIndexScanAsync(driverType, scanId);
            if (scan is null || scan.State.IsTerminal())
            {
                return null;
            }

            await AbortAsync(scan, delete: false);

            return scan;
        }

        public async Task DestroyOutputAsync(CatalogScanDriverType driverType)
        {
            var driver = _driverFactory.Create(driverType);
            await driver.DestroyOutputAsync();
        }

        private async Task AbortAsync(CatalogIndexScan scan, bool delete)
        {
            if (!await AbortInternalAsync(scan, CatalogScanDriverType.Internal_FindLatestCatalogLeafScan))
            {
                await AbortInternalAsync(scan, CatalogScanDriverType.Internal_FindLatestCatalogLeafScanPerId);
            }

            await _driverFactory.Create(scan.DriverType).FinalizeAsync(scan);
            await _storageService.DeleteChildTablesAsync(scan.StorageSuffix);
            await _taskStateStorageService.DeleteTableAsync(scan.StorageSuffix);

            if (delete)
            {
                scan.ETag = ETag.All;
                await _storageService.DeleteAsync(scan);
            }
            else
            {
                scan.ETag = ETag.All;
                scan.Completed = DateTimeOffset.UtcNow;
                scan.State = CatalogIndexScanState.Aborted;
                await _storageService.ReplaceAsync(scan);
            }
        }

        private async Task<bool> AbortInternalAsync(CatalogIndexScan scan, CatalogScanDriverType childDriverType)
        {
            var findLatestScanId = _storageService.GenerateFindLatestScanId(scan);
            var findLatestScan = await _storageService.GetIndexScanAsync(childDriverType, findLatestScanId);
            if (findLatestScan is not null)
            {
                await AbortAsync(findLatestScan, delete: true);
                return true;
            }

            return false;
        }

        public async Task<IReadOnlyDictionary<CatalogScanDriverType, CatalogScanServiceResult>> UpdateAllAsync(DateTimeOffset? max)
        {
            if (!max.HasValue)
            {
                max = await _cursorService.GetSourceMaxAsync();
            }

            var results = new Dictionary<CatalogScanDriverType, CatalogScanServiceResult>();
            foreach (var driverType in CatalogScanDriverMetadata.StartableDriverTypes)
            {
                var result = await UpdateAsync(driverType, max, onlyLatestLeaves: null, continueWithDependents: true);
                results.Add(driverType, result);
            }

            foreach (var pair in results)
            {
                switch (pair.Value.Type)
                {
                    case CatalogScanServiceResultType.NewStarted:
                        _logger.LogInformation("Started {DriverType} catalog scan {ScanId} with max {Max:O}.", pair.Key, pair.Value.Scan.ScanId, max.Value);
                        break;
                    default:
                        _logger.LogInformation("{DriverType} catalog scan did not start due to: {ResultType}.", pair.Key, pair.Value.Type);
                        break;
                }
            }

            return results;
        }

        public async Task<CatalogScanServiceResult> UpdateAsync(CatalogScanDriverType driverType)
        {
            return await UpdateAsync(driverType, max: null);
        }

        public async Task<CatalogScanServiceResult> UpdateAsync(CatalogScanDriverType driverType, DateTimeOffset? max)
        {
            return await UpdateAsync(driverType, max, onlyLatestLeaves: null);
        }

        public async Task<CatalogScanServiceResult> UpdateAsync(CatalogScanDriverType driverType, DateTimeOffset? max, bool? onlyLatestLeaves)
        {
            return await UpdateAsync(driverType, max, onlyLatestLeaves, continueWithDependents: false);
        }

        private async Task<CatalogScanServiceResult> UpdateAsync(CatalogScanDriverType driverType, DateTimeOffset? max, bool? onlyLatestLeaves, bool continueWithDependents)
        {
            var defaultMin = CatalogScanDriverMetadata.GetDefaultMin(driverType);

            var result = await UpdateAsync(
                driverType,
                onlyLatestLeaves,
                defaultMin,
                max,
                continueWithDependents);

            EmitResultMetric(driverType, result);

            return result;
        }

        private void EmitResultMetric(CatalogScanDriverType driverType, CatalogScanServiceResult result)
        {
            _telemetryClient.TrackMetric(
                MetricNames.CatalogScanUpdate,
                1,
                new Dictionary<string, string>
                {
                    { "DriverType", driverType.ToString() },
                    { "ResultType", result.Type.ToString() },
                    { "ScanId", result.Scan?.ScanId.ToString() },
                    { "RangeType", result.Scan?.BucketRanges is not null ? "Bucket" : "Commit" },
                    { "ParentDriverType", result.Scan?.ParentDriverType?.ToString() },
                    { "ParentScanId", result.Scan?.ParentScanId },
                });
        }

        public async Task<CatalogIndexScan> GetOrStartFindLatestCatalogLeafScanAsync(
            string scanId,
            string storageSuffix,
            CatalogScanDriverType parentDriverType,
            string parentScanId,
            DateTimeOffset min,
            DateTimeOffset max)
        {
            return await GetOrStartFindLatestCatalogLeafScanAsync(
                CatalogScanDriverType.Internal_FindLatestCatalogLeafScan,
                scanId,
                storageSuffix,
                parentDriverType,
                parentScanId,
                min,
                max);
        }

        public async Task<CatalogIndexScan> GetOrStartFindLatestCatalogLeafScanPerIdAsync(
            string scanId,
            string storageSuffix,
            CatalogScanDriverType parentDriverType,
            string parentScanId,
            DateTimeOffset min,
            DateTimeOffset max)
        {
            return await GetOrStartFindLatestCatalogLeafScanAsync(
                CatalogScanDriverType.Internal_FindLatestCatalogLeafScanPerId,
                scanId,
                storageSuffix,
                parentDriverType,
                parentScanId,
                min,
                max);
        }

        public async Task<CatalogScanServiceResult> UpdateAsync(
            CatalogScanDriverType driverType,
            IReadOnlyCollection<int> buckets)
        {
            var descendingId = StorageUtility.GenerateDescendingId();
            var scanId = GetBucketRangeScanId(buckets, descendingId);
            return await UpdateAsync(scanId, descendingId.Unique, driverType, buckets);
        }

        public async Task<CatalogScanServiceResult> UpdateAsync(
            string scanId,
            string storageSuffix,
            CatalogScanDriverType driverType,
            IReadOnlyCollection<int> buckets)
        {
            var result = await UpdateInternalAsync(scanId, storageSuffix, driverType, buckets);
            EmitResultMetric(driverType, result);
            return result;
        }

        public async Task<CatalogScanServiceResult> UpdateInternalAsync(
            string scanId,
            string storageSuffix,
            CatalogScanDriverType driverType,
            IEnumerable<int> buckets)
        {
            if (!CatalogScanDriverMetadata.GetBucketRangeSupport(driverType))
            {
                throw new ArgumentException($"The driver {driverType} is not supported for bucket range processing.", nameof(driverType));
            }

            var existing = await _storageService.GetIndexScanAsync(driverType, scanId);
            if (existing is not null)
            {
                return new CatalogScanServiceResult(CatalogScanServiceResultType.AlreadyStarted, dependencyName: null, existing);
            }

            var disabledOrStarted = await CheckForDisabledOrStartedAsync(driverType);
            if (disabledOrStarted is not null)
            {
                return disabledOrStarted;
            }

            var cursors = (await _cursorService.GetCursorsAsync()).ToDictionary(x => x.Key, x => x.Value.Value);
            var loadBucketedPackageCursor = cursors[CatalogScanDriverType.LoadBucketedPackage];
            foreach (var dependency in CatalogScanDriverMetadata.GetTransitiveClosure(driverType))
            {
                // If a driver or its dependencies hasn't caught up with LoadBucketedPackage, that means there may be
                // packages that haven't been processed, but have been loaded in to the bucketed packages table. If we
                // were to process the provided buckets with this driver, it may encounter packages that have never been
                // processed at all. The bucket-based processing is meant for reprocessing packages, not processing them
                // for the very first time.
                if (loadBucketedPackageCursor > cursors[dependency])
                {
                    return new CatalogScanServiceResult(
                        CatalogScanServiceResultType.BlockedByDependency,
                        dependency.ToString(),
                        scan: null);
                }
            }

            await using (var lease = await GetStartDriverLeaseAsync(driverType, parentDriverType: null))
            {
                if (!lease.Acquired)
                {
                    return new CatalogScanServiceResult(CatalogScanServiceResultType.UnavailableLease, dependencyName: null, scan: null);
                }

                disabledOrStarted = await CheckForDisabledOrStartedAsync(driverType);
                if (disabledOrStarted is not null)
                {
                    return disabledOrStarted;
                }

                // Set the catalog scan min and max. These aren't used for a proper catalog scan but may be used for logging or analysis purposes.
                var min = CatalogClient.NuGetOrgMinDeleted;
                var max = loadBucketedPackageCursor;

                var newScan = await StartWithoutLeaseAsync(
                    driverType,
                    scanId,
                    storageSuffix,
                    onlyLatestLeaves: true, // the Bucketed Packages table only keeps the latest catalog leaf per version
                    parentDriverType: null,
                    parentScanId: null,
                    cursorName: NoCursor,
                    min,
                    max,
                    bucketRanges: BucketRange.BucketsToRanges(buckets),
                    continueWithDependents: false);

                return new CatalogScanServiceResult(CatalogScanServiceResultType.NewStarted, dependencyName: null, newScan);
            }
        }

        public static string GetBucketRangeScanId(IEnumerable<int> buckets, StorageId storageId)
        {
            return $"{storageId}-r{buckets.Count()}";
        }

        private async Task<CatalogScanServiceResult> UpdateAsync(
            CatalogScanDriverType driverType,
            bool? onlyLatestLeaves,
            DateTimeOffset min,
            DateTimeOffset? max,
            bool continueWithDependents)
        {
            var onlyLatestLeavesSupport = CatalogScanDriverMetadata.GetOnlyLatestLeavesSupport(driverType);
            if (onlyLatestLeavesSupport.HasValue
                && onlyLatestLeaves.HasValue
                && onlyLatestLeaves != onlyLatestLeavesSupport)
            {
                if (onlyLatestLeavesSupport.Value)
                {
                    throw new ArgumentException($"Only using latest leaves is supported for driver {driverType}.", nameof(onlyLatestLeaves));
                }
                else
                {
                    throw new ArgumentException($"Only using all leaves is supported for driver {driverType}.", nameof(onlyLatestLeaves));
                }
            }

            if (!onlyLatestLeaves.HasValue)
            {
                onlyLatestLeaves = onlyLatestLeavesSupport.GetValueOrDefault(true);
            }

            var disabledOrStarted = await CheckForDisabledOrStartedAsync(driverType);
            if (disabledOrStarted is not null)
            {
                return disabledOrStarted;
            }

            var cursor = await _cursorService.GetCursorAsync(driverType);
            var usedDefaultMin = true;
            if (cursor.Value > CursorTableEntity.Min)
            {
                // Use the cursor value as the min if it's greater than the provided min. We don't want to process leaves
                // that have already been scanned.
                min = cursor.Value;
                usedDefaultMin = false;
            }

            (var dependencyName, var dependencyValue) = await _cursorService.GetMinDependencyCursorValueAsync(driverType);

            if (dependencyValue <= CursorTableEntity.Min)
            {
                return new CatalogScanServiceResult(CatalogScanServiceResultType.BlockedByDependency, dependencyName, scan: null);
            }

            var tookDependencyMax = false;
            if (!max.HasValue)
            {
                max = dependencyValue;
                tookDependencyMax = true;
            }
            else
            {
                if (max > dependencyValue)
                {
                    return new CatalogScanServiceResult(CatalogScanServiceResultType.BlockedByDependency, dependencyName, scan: null);
                }
            }

            if (usedDefaultMin && max < min)
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

            if (min == dependencyValue)
            {
                return new CatalogScanServiceResult(CatalogScanServiceResultType.FullyCaughtUpWithDependency, dependencyName, scan: null);
            }

            await using (var lease = await GetStartDriverLeaseAsync(driverType, parentDriverType: null))
            {
                if (!lease.Acquired)
                {
                    return new CatalogScanServiceResult(CatalogScanServiceResultType.UnavailableLease, dependencyName: null, scan: null);
                }

                disabledOrStarted = await CheckForDisabledOrStartedAsync(driverType);
                if (disabledOrStarted is not null)
                {
                    return disabledOrStarted;
                }

                var descendingId = StorageUtility.GenerateDescendingId();
                var newScan = await StartWithoutLeaseAsync(
                    driverType,
                    descendingId.ToString(),
                    descendingId.Unique,
                    onlyLatestLeaves.Value,
                    parentDriverType: null,
                    parentScanId: null,
                    cursor.Name,
                    min,
                    max.Value,
                    bucketRanges: null,
                    continueWithDependents);

                return new CatalogScanServiceResult(CatalogScanServiceResultType.NewStarted, dependencyName: null, newScan);
            }
        }

        private async Task<CatalogScanServiceResult> CheckForDisabledOrStartedAsync(CatalogScanDriverType driverType)
        {
            if (!IsEnabled(driverType))
            {
                return new CatalogScanServiceResult(CatalogScanServiceResultType.Disabled, dependencyName: null, scan: null);
            }

            // Check if a scan is already started, outside the lease.
            var incompleteScan = await GetLatestIncompleteScanAsync(driverType);
            if (incompleteScan != null)
            {
                return new CatalogScanServiceResult(CatalogScanServiceResultType.AlreadyStarted, dependencyName: null, incompleteScan);
            }

            return null;
        }

        private async Task<CatalogIndexScan> GetOrStartFindLatestCatalogLeafScanAsync(
            CatalogScanDriverType driverType,
            string scanId,
            string storageSuffix,
            CatalogScanDriverType parentDriverType,
            string parentScanId,
            DateTimeOffset min,
            DateTimeOffset max)
        {
            // Check if a scan is already started, outside the lease.
            var incompleteScan = await _storageService.GetIndexScanAsync(driverType, scanId);
            if (incompleteScan != null)
            {
                return incompleteScan;
            }

            // Use a rather generic lease, to simplify clean-up.
            await using (var lease = await GetStartDriverLeaseAsync(driverType, parentDriverType))
            {
                if (!lease.Acquired)
                {
                    throw new InvalidOperationException("Another thread is already starting the scan.");
                }

                // Check if a scan is already running, inside the lease.
                incompleteScan = await _storageService.GetIndexScanAsync(driverType, scanId);
                if (incompleteScan != null)
                {
                    return incompleteScan;
                }

                return await StartWithoutLeaseAsync(
                    driverType,
                    scanId,
                    storageSuffix,
                    onlyLatestLeaves: false,
                    parentDriverType,
                    parentScanId,
                    NoCursor,
                    min,
                    max,
                    bucketRanges: null,
                    continueWithDependents: false);
            }
        }

        private async Task<CatalogIndexScan> StartWithoutLeaseAsync(
            CatalogScanDriverType driverType,
            string scanId,
            string storageSuffix,
            bool onlyLatestLeaves,
            CatalogScanDriverType? parentDriverType,
            string parentScanId,
            string cursorName,
            DateTimeOffset min,
            DateTimeOffset max,
            string bucketRanges,
            bool continueWithDependents)
        {
            // Start a new scan.
            _logger.LogInformation("Attempting to start a {DriverType} catalog index scan from ({Min:O}, {Max:O}] with scan ID {ScanId}.", driverType, min, max, scanId);

            var catalogIndexScanMessage = new CatalogIndexScanMessage
            {
                DriverType = driverType,
                ScanId = scanId,
            };
            await _messageEnqueuer.EnqueueAsync(new[] { catalogIndexScanMessage });
            var catalogIndexScan = new CatalogIndexScan(driverType, scanId, storageSuffix)
            {
                State = CatalogIndexScanState.Created,
                OnlyLatestLeaves = onlyLatestLeaves,
                ParentDriverType = parentDriverType,
                ParentScanId = parentScanId,
                CursorName = cursorName,
                Min = min,
                Max = max,
                BucketRanges = bucketRanges,
                ContinueUpdate = continueWithDependents,
            };
            await _storageService.InsertAsync(catalogIndexScan);

            return catalogIndexScan;
        }

        private async Task<CatalogIndexScan> GetLatestIncompleteScanAsync(CatalogScanDriverType driverType)
        {
            var latestScans = await _storageService.GetLatestIndexScansAsync(driverType, maxEntities: 20);
            var incompleteScans = latestScans.Where(x => !x.State.IsTerminal());
            if (incompleteScans.Any())
            {
                return incompleteScans.First();
            }

            return null;
        }

        private async Task<AutoRenewingStorageLeaseResult> GetStartDriverLeaseAsync(CatalogScanDriverType driverType, CatalogScanDriverType? parentDriverType)
        {
            var leaseName = $"Start-{driverType}";
            if (parentDriverType.HasValue)
            {
                leaseName += $"-{parentDriverType}";
            }

            return await _leaseService.TryAcquireWithRetryAsync(leaseName);
        }
    }
}

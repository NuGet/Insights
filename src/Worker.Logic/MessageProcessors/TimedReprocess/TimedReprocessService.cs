// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Microsoft.Extensions.Options;
using NuGet.Insights.Worker.LoadBucketedPackage;

namespace NuGet.Insights.Worker.TimedReprocess
{
    public class TimedReprocessService
    {
        private static bool HasUpdatesOutsideOfCatalog(CatalogScanDriverType type)
        {
            return type switch
            {
                // README is not always embedded in the package and can be updated without a catalog update.
                CatalogScanDriverType.LoadPackageReadme => true,

                // Symbol package files (.snupkg) can be replaced and removed without a catalog update.
                CatalogScanDriverType.LoadSymbolPackageArchive => true,

                // Internally the NPE analysis APIs read symbols from the Microsoft and NuGet.org symbol servers. This
                // means that the results are unstable for a similar reason as LoadSymbolPackageArchive. Additionally,
                // some analysis times out (NuGetPackageExplorerResultType.Timeout). However this driver is relatively
                // costly and slow to run. Therefore we won't consider it for reprocessing.
                CatalogScanDriverType.NuGetPackageExplorerToCsv => false,

                // This driver uses a compatibility map baked into NuGet/NuGetGallery which uses the NuGet.Frameworks
                // package for framework compatibility. We could choose to periodically reprocess package compatibility
                // so that changes in TFM mapping and computed frameworks automatically get picked up. For now, we won't
                // do that and force a manual "Reset" operation from the admin panel to recompute all compatibilities.
                // The main part of this data that changes is computed compatibility. Newly supported frameworks can
                // also lead to changes in results, but this would take a package owner guessing or colliding with this
                // new framework in advance, leading to an "unsupported" framework reparsing as a supported framework.
                CatalogScanDriverType.PackageCompatibilityToCsv => false,

                // Similar to PackageCompatibilityToCsv, an unsupported framework (or an existing framework) can become
                // supported or change its interpretion over time. This is pretty unlikely so we don't reprocess
                // this driver.
                CatalogScanDriverType.PackageAssetToCsv => false,

                // Certificate data is not stable because certificates can expire or be revoked. Also, certificate
                // chain resolution is non-deterministic, so different intermediate certificates can be resolved over
                // time. Despite this, the changes are not to significant over time so we won't reprocess.
                CatalogScanDriverType.PackageCertificateToCsv => false,

                // If an SPDX support license becomes deprecated, the results of this driver will change when the
                // NuGet.Package dependency is updated. This is rare, so we won't reprocess.
                CatalogScanDriverType.PackageLicenseToCsv => false,

                // Changes to the hosted icon for a package occur along with a catalog update, even package with icon
                // URL (non-embedded icon) because the Catalog2Icon job follows the catalog. The data could be unstable
                // if NuGet Insights runs before Catalog2Icon does (unlikely) or if the Magick.NET dependency is updated.
                // In that case, the driver can be manually rerun with the "Reset" button on the admin panel.
                CatalogScanDriverType.PackageIconToCsv => false,

                _ => false
            };
        }
        
        private readonly BucketedPackageService _bucketedPackageService;
        private readonly TimedReprocessStorageService _storageService;
        private readonly AutoRenewingStorageLeaseService _leaseService;
        private readonly CatalogScanService _catalogScanService;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public TimedReprocessService(
            BucketedPackageService bucketedPackageService,
            TimedReprocessStorageService storageService,
            AutoRenewingStorageLeaseService leaseService,
            CatalogScanService catalogScanService, 
            IMessageEnqueuer messageEnqueuer,
            IOptions<NuGetInsightsWorkerSettings> options)
        {
            _bucketedPackageService = bucketedPackageService;
            _storageService = storageService;
            _leaseService = leaseService;
            _catalogScanService = catalogScanService;
            _messageEnqueuer = messageEnqueuer;
            _options = options;
        }

        public async Task InitializeAsync()
        {
            await _bucketedPackageService.InitializeAsync();
            await _storageService.InitializeAsync();
            await _leaseService.InitializeAsync();
            await _messageEnqueuer.InitializeAsync();
        }

        public IReadOnlyList<IReadOnlyList<CatalogScanDriverType>> GetReprocessBatches()
        {
            var reprocess = CatalogScanCursorService
                .StartableDriverTypes
                .Where(HasUpdatesOutsideOfCatalog)
                .ToHashSet();

            var isOrDependsOnReprocess = CatalogScanCursorService
                .StartableDriverTypes
                .Where(x => CatalogScanCursorService.GetTransitiveClosure(x).Any(reprocess.Contains))
                .ToList();

            // key: driver, value: direct dependencies
            var graph = new Dictionary<CatalogScanDriverType, HashSet<CatalogScanDriverType>>();
            foreach (var self in isOrDependsOnReprocess)
            {
                if (_options.Value.DisabledDrivers.Contains(self))
                {
                    continue;
                }

                graph.Add(self, CatalogScanCursorService.GetDependencies(self).ToHashSet());
            }

            // Collect batches of drivers that can run in parallel.
            var batches = new List<IReadOnlyList<CatalogScanDriverType>>();
            while (graph.Count > 0)
            {
                var batch = graph.Where(pair => pair.Value.Count == 0).Select(x => x.Key).ToHashSet();
                if (batch.Count == 0)
                {
                    var unresolved = graph
                        .Select(pair => new { Driver = pair.Key, Missing = pair.Value.Where(x => !graph.ContainsKey(x)).Order().ToList() })
                        .Where(x => x.Missing.Count > 0)
                        .OrderBy(x => x.Driver)
                        .Select(x => $"{x.Driver} depends on {string.Join(", ", x.Missing)}")
                        .ToList();

                    throw new InvalidOperationException($"Check the {nameof(NuGetInsightsWorkerSettings)}.{nameof(NuGetInsightsWorkerSettings.DisabledDrivers)} option. Some drivers are missing dependencies: {string.Join("; ", unresolved)}");
                }

                foreach (var type in batch)
                {
                    graph.Remove(type);
                }

                foreach (var dependencies in graph.Values)
                {
                    dependencies.ExceptWith(batch);
                }

                // Only keep drivers that need reprocessing or drivers that depend on a driver that needs reprocessing.
                batch.IntersectWith(isOrDependsOnReprocess);
                if (batch.Count > 0)
                {
                    batches.Add(batch.OrderBy(x => x.ToString()).ToList());
                }
            }

            return batches;
        }

        public async Task AbortAsync()
        {
            var runs = await _storageService.GetRunsAsync();
            var latestRun = runs.MaxBy(x => x.Created);
            if (latestRun is null || latestRun.State.IsTerminal())
            {
                return;
            }

            var scans = await _storageService.GetScansAsync(latestRun.RunId);
            foreach (var scan in scans)
            {
                await _catalogScanService.AbortAsync(scan.DriverType, scan.ScanId);
            }

            latestRun.ETag = ETag.All;
            latestRun.Completed = DateTimeOffset.UtcNow;
            latestRun.State = TimedReprocessState.Aborted;
            await _storageService.ReplaceRunAsync(latestRun);
        }

        public async Task<TimedReprocessRun> StartAsync()
        {
            return await StartAsync(buckets: null);
        }

        public async Task<TimedReprocessRun> StartAsync(IReadOnlyList<int> buckets)
        {
            await using var lease = await _leaseService.TryAcquireAsync("Start-TimedReprocess");
            if (!lease.Acquired)
            {
                return null;
            }

            if (await IsAnyTimedReprocessRunningAsync())
            {
                return null;
            }

            if (buckets == null)
            {
                buckets = (await _storageService.GetBucketsToReprocessAsync()).Select(x => x.Index).ToList();
                if (buckets.Count == 0)
                {
                    // no more work to do, return the latest run.
                    var runs = await _storageService.GetLatestRunsAsync(1);
                    return runs.Single();
                }
            }

            var run = new TimedReprocessRun(StorageUtility.GenerateDescendingId().ToString(), buckets);
            await _messageEnqueuer.EnqueueAsync(new[]
            {
                new TimedReprocessMessage
                {
                    RunId = run.RunId,
                }
            });
            await _storageService.AddRunAsync(run);
            return run;
        }

        public async Task<bool> IsAnyTimedReprocessRunningAsync()
        {
            var runs = await _storageService.GetRunsAsync();
            return runs.Any(x => !x.State.IsTerminal());
        }
    }
}

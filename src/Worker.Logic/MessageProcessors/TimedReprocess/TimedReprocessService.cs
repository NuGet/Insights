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
            var dependsOnReprocess = CatalogScanDriverMetadata.StartableDriverTypes
                .Where(x => CatalogScanDriverMetadata.GetTransitiveClosure(x).Any(CatalogScanDriverMetadata.GetUpdatedOutsideOfCatalog))
                .ToHashSet();

            return CatalogScanDriverMetadata.GetParallelBatches(dependsOnReprocess, _options.Value.DisabledDrivers.ToHashSet());
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

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NuGet.Insights.Worker
{
    public class CatalogScanUpdateTimer : ITimer
    {
        private readonly CatalogScanService _catalogScanService;
        private readonly CatalogScanStorageService _catalogScanStorageService;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ILogger<CatalogScanUpdateTimer> _logger;

        public CatalogScanUpdateTimer(
            CatalogScanService catalogScanService,
            CatalogScanStorageService catalogScanStorageService,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger<CatalogScanUpdateTimer> logger)
        {
            _catalogScanService = catalogScanService;
            _catalogScanStorageService = catalogScanStorageService;
            _options = options;
            _logger = logger;
        }

        public string Name => "CatalogScanUpdate";
        public TimeSpan Frequency => _options.Value.CatalogScanUpdateFrequency;
        public bool AutoStart => _options.Value.AutoStartCatalogScanUpdate;
        public bool IsEnabled => true;
        public int Order => 10;
        public bool CanAbort => true;

        public async Task AbortAsync()
        {
            await _catalogScanService.AbortAllAsync();
        }

        public async Task<bool> ExecuteAsync()
        {
            var indexScans = await _catalogScanStorageService.GetIndexScansAsync();
            var typesWithLatestAborted = indexScans
                .GroupBy(x => x.DriverType)
                .Select(x => x.MaxBy(x => x.Completed))
                .Where(x => x?.State == CatalogIndexScanState.Aborted)
                .Select(x => x.DriverType)
                .ToList();
            if (typesWithLatestAborted.Count > 0)
            {
                _logger.LogWarning(
                    "The catalog scan timer failed to start because the latest catalog scan for at least one driver was aborted. " +
                    "It must be run manually first. Aborted drivers: {DriverTypes}", typesWithLatestAborted);
                return false;
            }

            var results = await _catalogScanService.UpdateAllAsync(max: null);
            var newStartedOrCaughtUp = results.Values.Any(x => x.Type == CatalogScanServiceResultType.NewStarted)
                || results.Values.All(x => x.Type == CatalogScanServiceResultType.Disabled
                                        || x.Type == CatalogScanServiceResultType.FullyCaughtUpWithMax);
            var resultTypes = results
                .GroupBy(x => x.Value.Type)
                .ToDictionary(x => x.Key, x => x.Count())
                .OrderByDescending(x => x.Value)
                .Select(x => $"{x.Key} ({x.Value}x)")
                .ToList();

            if (newStartedOrCaughtUp)
            {
                _logger.LogInformation("At least one catalog scan was started or they are all caught up. Driver results: {Results}", resultTypes);
                return true;
            }
            else
            {
                _logger.LogWarning("No new catalog scan could be started but they aren't caught up. Driver results: {Results}", resultTypes);
                return false;
            }
        }

        public async Task InitializeAsync()
        {
            await _catalogScanService.InitializeAsync();
        }

        public async Task<bool> IsRunningAsync()
        {
            var indexScans = await _catalogScanStorageService.GetIndexScansAsync();
            return indexScans.Any(x => !x.State.IsTerminal());
        }
    }
}

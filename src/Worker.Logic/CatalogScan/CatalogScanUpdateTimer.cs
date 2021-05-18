// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace NuGet.Insights.Worker
{
    public class CatalogScanUpdateTimer : ITimer
    {
        private readonly CatalogScanService _catalogScanService;
        private readonly CatalogScanStorageService _catalogScanStorageService;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public CatalogScanUpdateTimer(
            CatalogScanService catalogScanService,
            CatalogScanStorageService catalogScanStorageService,
            IOptions<NuGetInsightsWorkerSettings> options)
        {
            _catalogScanService = catalogScanService;
            _catalogScanStorageService = catalogScanStorageService;
            _options = options;
        }

        public string Name => "CatalogScanUpdate";
        public TimeSpan Frequency => _options.Value.CatalogScanUpdateFrequency;
        public bool AutoStart => _options.Value.AutoStartCatalogScanUpdate;
        public bool IsEnabled => true;
        public int Order => 10;

        public async Task<bool> ExecuteAsync()
        {
            var results = await _catalogScanService.UpdateAllAsync(max: null);
            return results.Values.Any(x => x.Type == CatalogScanServiceResultType.NewStarted);
        }

        public async Task InitializeAsync()
        {
            await _catalogScanService.InitializeAsync();
        }

        public async Task<bool> IsRunningAsync()
        {
            var indexScans = await _catalogScanStorageService.GetIndexScansAsync();
            return indexScans.Any(x => x.State != CatalogIndexScanState.Complete);
        }
    }
}

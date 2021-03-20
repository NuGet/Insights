using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogScanUpdateTimer : ITimer
    {
        private readonly CatalogScanService _catalogScanService;
        private readonly CatalogScanStorageService _catalogScanStorageService;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;

        public CatalogScanUpdateTimer(
            CatalogScanService catalogScanService,
            CatalogScanStorageService catalogScanStorageService,
            IOptions<ExplorePackagesWorkerSettings> options)
        {
            _catalogScanService = catalogScanService;
            _catalogScanStorageService = catalogScanStorageService;
            _options = options;
        }

        public string Name => "CatalogScanUpdate";
        public TimeSpan Frequency => _options.Value.CatalogScanUpdateFrequency;
        public bool AutoStart => _options.Value.AutoStartCatalogScanUpdate;
        public bool IsEnabled => true;

        public async Task ExecuteAsync()
        {
            await _catalogScanService.UpdateAllAsync(max: null);
        }

        public async Task InitializeAsync()
        {
            await _catalogScanService.InitializeAsync();
        }

        public async Task<bool> IsRunningAsync()
        {
            var indexScans = await _catalogScanStorageService.GetIndexScansAsync();
            return indexScans.Any(x => x.ParsedState != CatalogIndexScanState.Complete);
        }
    }
}

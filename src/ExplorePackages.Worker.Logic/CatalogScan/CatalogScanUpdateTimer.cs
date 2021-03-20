using System;
using System.Linq;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogScanUpdateTimer : ITimer
    {
        private readonly CatalogScanService _catalogScanService;
        private readonly CatalogScanStorageService _catalogScanStorageService;

        public CatalogScanUpdateTimer(
            CatalogScanService catalogScanService,
            CatalogScanStorageService catalogScanStorageService)
        {
            _catalogScanService = catalogScanService;
            _catalogScanStorageService = catalogScanStorageService;
        }

        public string Name => "CatalogScanUpdate";
        public TimeSpan Frequency => TimeSpan.FromHours(1);
        public bool AutoStart => false;
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

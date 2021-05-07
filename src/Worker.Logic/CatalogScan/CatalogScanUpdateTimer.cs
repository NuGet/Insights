using System;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Worker.KustoIngestion;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogScanUpdateTimer : ITimer
    {
        private readonly CatalogScanService _catalogScanService;
        private readonly CatalogScanStorageService _catalogScanStorageService;
        private readonly KustoIngestionService _kustoIngestionService;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;

        public CatalogScanUpdateTimer(
            CatalogScanService catalogScanService,
            CatalogScanStorageService catalogScanStorageService,
            KustoIngestionService kustoIngestionService,
            IOptions<ExplorePackagesWorkerSettings> options)
        {
            _catalogScanService = catalogScanService;
            _catalogScanStorageService = catalogScanStorageService;
            _kustoIngestionService = kustoIngestionService;
            _options = options;
        }

        public string Name => "CatalogScanUpdate";
        public TimeSpan Frequency => _options.Value.CatalogScanUpdateFrequency;
        public bool AutoStart => _options.Value.AutoStartCatalogScanUpdate;
        public bool IsEnabled => true;
        public int Precedence => default;

        public async Task<bool> ExecuteAsync()
        {
            var executed = false;

            // We don't want to start automatic catalog scans if the Kusto ingestion is running. This could lead to
            // partial data in Kusto since a CSV blob could be imported prior to the catalog scan aggregating its
            // results in the blob upon completion.
            await _kustoIngestionService.ExecuteIfNoIngestionIsRunningAsync(async () =>
            {
                var results = await _catalogScanService.UpdateAllAsync(max: null);
                executed = results.Values.Any(x => x.Type == CatalogScanServiceResultType.NewStarted);
            });

            return executed;
        }

        public async Task InitializeAsync()
        {
            await _catalogScanService.InitializeAsync();
            await _kustoIngestionService.InitializeAsync();
        }

        public async Task<bool> IsRunningAsync()
        {
            var indexScans = await _catalogScanStorageService.GetIndexScansAsync();
            return indexScans.Any(x => x.State != CatalogIndexScanState.Complete);
        }
    }
}

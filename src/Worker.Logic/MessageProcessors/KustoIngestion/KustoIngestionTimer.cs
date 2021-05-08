using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Worker.KustoIngestion
{
    public class KustoIngestionTimer : ITimer
    {
        public const string DefaultName = "KustoIngestion";

        private readonly KustoIngestionService _kustoIngestionService;
        private readonly KustoIngestionStorageService _kustoIngestionStorageService;
        private readonly CatalogScanService _catalogScanService;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;

        public KustoIngestionTimer(
            KustoIngestionService kustoIngestionService,
            KustoIngestionStorageService kustoIngestionStorageService,
            CatalogScanService catalogScanService,
            IOptions<ExplorePackagesWorkerSettings> options)
        {
            _kustoIngestionService = kustoIngestionService;
            _kustoIngestionStorageService = kustoIngestionStorageService;
            _catalogScanService = catalogScanService;
            _options = options;
        }

        public string Name => DefaultName;
        public TimeSpan Frequency => TimeSpan.FromDays(1);
        public bool AutoStart => false;
        public bool IsEnabled => _options.Value.KustoConnectionString != null && _options.Value.KustoDatabaseName != null;
        public int Precedence => default;

        public async Task InitializeAsync()
        {
            await _kustoIngestionService.InitializeAsync();
            await _catalogScanService.InitializeAsync();
        }

        public async Task<bool> ExecuteAsync()
        {
            var executed = false;

            await _catalogScanService.ExecuteIfNoScansAreRunningAsync(async () =>
            {
                var ingestion = await _kustoIngestionService.StartAsync();
                executed = ingestion is not null;
            });

            return executed;
        }

        public async Task<bool> IsRunningAsync()
        {
            return await _kustoIngestionStorageService.IsIngestionRunningAsync();
        }
    }
}

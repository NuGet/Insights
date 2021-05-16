using System;
using System.Threading.Tasks;

namespace NuGet.Insights.Worker.KustoIngestion
{
    public class KustoIngestionTimer : ITimer
    {
        private readonly KustoIngestionService _kustoIngestionService;
        private readonly KustoIngestionStorageService _kustoIngestionStorageService;

        public KustoIngestionTimer(
            KustoIngestionService kustoIngestionService,
            KustoIngestionStorageService kustoIngestionStorageService)
        {
            _kustoIngestionService = kustoIngestionService;
            _kustoIngestionStorageService = kustoIngestionStorageService;
        }

        public string Name => "KustoIngestion";
        public TimeSpan Frequency => TimeSpan.FromDays(1);
        public bool AutoStart => false;
        public bool IsEnabled => _kustoIngestionService.HasRequiredConfiguration;
        public int Order => 30;

        public async Task InitializeAsync()
        {
            await _kustoIngestionService.InitializeAsync();
        }

        public async Task<bool> ExecuteAsync()
        {
            var ingestion = await _kustoIngestionService.StartAsync();
            return ingestion is not null;
        }

        public async Task<bool> IsRunningAsync()
        {
            return await _kustoIngestionStorageService.IsIngestionRunningAsync();
        }
    }
}

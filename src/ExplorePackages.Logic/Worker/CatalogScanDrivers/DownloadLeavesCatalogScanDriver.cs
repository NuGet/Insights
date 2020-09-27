using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class DownloadLeavesCatalogScanDriver : ICatalogScanDriver
    {
        private readonly CatalogClient _catalogClient;
        private readonly ILogger<DownloadLeavesCatalogScanDriver> _logger;

        public DownloadLeavesCatalogScanDriver(
            CatalogClient catalogClient,
            ILogger<DownloadLeavesCatalogScanDriver> logger)
        {
            _catalogClient = catalogClient;
            _logger = logger;
        }

        public Task<CatalogIndexScanResult> ProcessIndexAsync(CatalogIndexScan indexScan)
        {
            return Task.FromResult(CatalogIndexScanResult.Expand);
        }

        public Task<CatalogPageScanResult> ProcessPageAsync(CatalogPageScan pageScan)
        {
            return Task.FromResult(CatalogPageScanResult.Expand);
        }

        public async Task ProcessLeafAsync(CatalogLeafScan leafScan)
        {
            _logger.LogInformation("Loading catalog {Type} leaf URL: {Url}", leafScan.LeafType, leafScan.Url);
            await _catalogClient.GetCatalogLeafAsync(leafScan.ParsedLeafType, leafScan.Url);
        }

        public Task StartAggregateAsync(CatalogIndexScan indexScan) => Task.CompletedTask;
        public Task<bool> IsAggregateCompleteAsync(CatalogIndexScan indexScan) => Task.FromResult(true);
    }
}

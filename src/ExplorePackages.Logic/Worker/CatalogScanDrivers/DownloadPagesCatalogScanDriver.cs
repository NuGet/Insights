using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class DownloadPagesCatalogScanDriver : ICatalogScanDriver
    {
        private readonly CatalogClient _catalogClient;
        private readonly ILogger<DownloadLeavesCatalogScanDriver> _logger;

        public DownloadPagesCatalogScanDriver(
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

        public async Task<CatalogPageScanResult> ProcessPageAsync(CatalogPageScan pageScan)
        {
            _logger.LogInformation("Loading catalog page URL: {Url}", pageScan.Url);
            await _catalogClient.GetCatalogPageAsync(pageScan.Url);
            return CatalogPageScanResult.Processed;
        }

        public Task ProcessLeafAsync(CatalogLeafScan leafScan) => throw new NotImplementedException();
        public Task StartAggregateAsync(CatalogIndexScan indexScan) => Task.CompletedTask;
        public Task<bool> IsAggregateCompleteAsync(CatalogIndexScan indexScan) => Task.FromResult(true);
        public Task FinalizeAsync(CatalogIndexScan indexScan) => Task.CompletedTask;
    }
}

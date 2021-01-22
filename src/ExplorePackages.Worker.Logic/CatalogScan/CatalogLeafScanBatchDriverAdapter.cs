using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogLeafScanBatchDriverAdapter : ICatalogLeafScanBatchDriver
    {
        private readonly ICatalogLeafScanDriver _inner;
        private readonly ILogger<CatalogLeafScanBatchDriverAdapter> _logger;

        public CatalogLeafScanBatchDriverAdapter(ICatalogLeafScanDriver inner, ILogger<CatalogLeafScanBatchDriverAdapter> logger)
        {
            _inner = inner;
            _logger = logger;
        }

        public Task<CatalogIndexScanResult> ProcessIndexAsync(CatalogIndexScan indexScan) => _inner.ProcessIndexAsync(indexScan);

        public Task<CatalogPageScanResult> ProcessPageAsync(CatalogPageScan pageScan) => _inner.ProcessPageAsync(pageScan);

        public async Task<BatchMessageProcessorResult<CatalogLeafScan>> ProcessLeavesAsync(IReadOnlyList<CatalogLeafScan> leafScans)
        {
            var tryAgainLater = new List<(CatalogLeafScan Scan, TimeSpan NotBefore)>();
            foreach (var leafScan in leafScans)
            {
                try
                {
                    var result = await _inner.ProcessLeafAsync(leafScan);
                    switch (result.Type)
                    {
                        case DriverResultType.TryAgainLater:
                            _logger.LogWarning("A catalog leaf scan will need to be tried again later.");
                            tryAgainLater.Add((leafScan, TimeSpan.FromMinutes(1)));
                            break;
                        case DriverResultType.Success:
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "A catalog leaf scan failed.");
                    tryAgainLater.Add((leafScan, TimeSpan.Zero));
                }
            }

            return new BatchMessageProcessorResult<CatalogLeafScan>(tryAgainLater);
        }

        public Task StartAggregateAsync(CatalogIndexScan indexScan) => _inner.StartAggregateAsync(indexScan);

        public Task<bool> IsAggregateCompleteAsync(CatalogIndexScan indexScan) => _inner.IsAggregateCompleteAsync(indexScan);

        public Task FinalizeAsync(CatalogIndexScan indexScan) => _inner.FinalizeAsync(indexScan);
    }
}

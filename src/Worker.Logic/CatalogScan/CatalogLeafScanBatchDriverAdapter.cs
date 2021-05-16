using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Insights.Worker
{
    public class CatalogLeafScanBatchDriverAdapter : ICatalogLeafScanBatchDriver
    {
        private readonly ICatalogLeafScanNonBatchDriver _inner;
        private readonly ILogger<CatalogLeafScanBatchDriverAdapter> _logger;

        public CatalogLeafScanBatchDriverAdapter(ICatalogLeafScanNonBatchDriver inner, ILogger<CatalogLeafScanBatchDriverAdapter> logger)
        {
            _inner = inner;
            _logger = logger;
        }

        public async Task InitializeAsync(CatalogIndexScan indexScan)
        {
            await _inner.InitializeAsync(indexScan);
        }

        public Task<CatalogIndexScanResult> ProcessIndexAsync(CatalogIndexScan indexScan)
        {
            return _inner.ProcessIndexAsync(indexScan);
        }

        public Task<CatalogPageScanResult> ProcessPageAsync(CatalogPageScan pageScan)
        {
            return _inner.ProcessPageAsync(pageScan);
        }

        public async Task<BatchMessageProcessorResult<CatalogLeafScan>> ProcessLeavesAsync(IReadOnlyList<CatalogLeafScan> leafScans)
        {
            var failed = new List<CatalogLeafScan>();
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
                catch (Exception ex) when (leafScans.Count != 1)
                {
                    _logger.LogError(ex, "A catalog leaf scan failed.");
                    failed.Add(leafScan);
                }
            }

            return new BatchMessageProcessorResult<CatalogLeafScan>(failed, tryAgainLater);
        }

        public Task StartAggregateAsync(CatalogIndexScan indexScan)
        {
            return _inner.StartAggregateAsync(indexScan);
        }

        public Task<bool> IsAggregateCompleteAsync(CatalogIndexScan indexScan)
        {
            return _inner.IsAggregateCompleteAsync(indexScan);
        }

        public Task FinalizeAsync(CatalogIndexScan indexScan)
        {
            return _inner.FinalizeAsync(indexScan);
        }

        public Task StartCustomExpandAsync(CatalogIndexScan indexScan)
        {
            return _inner.StartCustomExpandAsync(indexScan);
        }

        public Task<bool> IsCustomExpandCompleteAsync(CatalogIndexScan indexScan)
        {
            return _inner.IsCustomExpandCompleteAsync(indexScan);
        }
    }
}

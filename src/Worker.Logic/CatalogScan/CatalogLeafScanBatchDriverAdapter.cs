// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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

        public async Task InitializeAsync()
        {
            await _inner.InitializeAsync();
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
            return await ProcessLeavesOneByOneAsync(leafScans, ProcessLeafAsync, _ => { }, _logger);
        }

        private async Task<(DriverResult, bool)> ProcessLeafAsync(CatalogLeafScan leafScan)
        {
            var result = await _inner.ProcessLeafAsync(leafScan);
            return (result, true);
        }

        public static async Task<BatchMessageProcessorResult<CatalogLeafScan>> ProcessLeavesOneByOneAsync<T>(
            IReadOnlyList<CatalogLeafScan> leafScans,
            Func<CatalogLeafScan, Task<(DriverResult, T)>> processLeafAsync,
            Action<T> handleValue,
            ILogger logger)
        {
            var failed = new List<CatalogLeafScan>();
            var tryAgainLater = new List<(CatalogLeafScan Scan, TimeSpan NotBefore)>();
            foreach (var leafScan in leafScans)
            {
                try
                {
                    var (result, value) = await processLeafAsync(leafScan);
                    switch (result.Type)
                    {
                        case DriverResultType.TryAgainLater:
                            logger.LogInformation("A catalog leaf scan for driver {DriverType} and scan {ScanId} will need to be tried again later.", leafScan.DriverType, leafScan.ScanId);
                            tryAgainLater.Add((leafScan, CatalogLeafScanMessageProcessor.TryAgainLaterDuration));
                            break;
                        case DriverResultType.Success:
                            handleValue(value);
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                }
                catch (Exception ex) when (leafScans.Count != 1)
                {
                    logger.LogError(ex, "A catalog leaf scan for driver {DriverType} and scan {ScanId} failed.", leafScan.DriverType, leafScan.ScanId);
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

        public Task DestroyOutputAsync()
        {
            return _inner.DestroyOutputAsync();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Worker.FindCatalogLeafItem
{
    public class FindCatalogLeafItemDriver : ICatalogScanDriver, ICatalogLeafScanNonBatchDriver, ICsvCompactor<CatalogLeafItemRecord>
    {
        private readonly CatalogScanToCsvAdapter<CatalogLeafItemRecord> _adapter;
        private readonly CatalogClient _catalogClient;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;

        public FindCatalogLeafItemDriver(
            CatalogScanToCsvAdapter<CatalogLeafItemRecord> adapter,
            CatalogClient catalogClient,
            IOptions<ExplorePackagesWorkerSettings> options)
        {
            _adapter = adapter;
            _catalogClient = catalogClient;
            _options = options;
        }

        public string ResultsContainerName => _options.Value.CatalogLeafItemContainerName;

        public List<CatalogLeafItemRecord> Prune(List<CatalogLeafItemRecord> records)
        {
            return records
                .Distinct()
                .OrderBy(x => x.CommitTimestamp)
                .ThenBy(x => x.Id)
                .ThenBy(x => x.LowerNormalizedVersion)
                .ToList();
        }

        public async Task<CatalogIndexScanResult> ProcessIndexAsync(CatalogIndexScan indexScan)
        {
            await _adapter.InitializeAsync(indexScan, ResultsContainerName);
            return CatalogIndexScanResult.ExpandAllLeaves;
        }

        public async Task<CatalogPageScanResult> ProcessPageAsync(CatalogPageScan pageScan)
        {
            var page = await _catalogClient.GetCatalogPageAsync(pageScan.Url);
            var items = page.GetLeavesInBounds(pageScan.Min, pageScan.Max, excludeRedundantLeaves: false);
            var records = items
                .Select(x => new CatalogLeafItemRecord
                {
                    CommitId = x.CommitId,
                    CommitTimestamp = x.CommitTimestamp,
                    Id = x.PackageId,
                    Version = x.PackageVersion,
                    Type = x.Type,
                    Url = x.Url,

                    LowerId = x.PackageId.ToLowerInvariant(),
                    LowerNormalizedVersion = x.ParsePackageVersion().ToNormalizedString(),

                    PageUrl = pageScan.Url,
                })
                .ToList();
            await _adapter.AppendAsync(
                pageScan.StorageSuffix,
                _options.Value.AppendResultStorageBucketCount,
                pageScan.Url,
                records);

            return CatalogPageScanResult.Processed;
        }

        public Task<DriverResult> ProcessLeafAsync(CatalogLeafScan leafScan) => throw new NotImplementedException();
        public Task StartAggregateAsync(CatalogIndexScan indexScan) => _adapter.StartAggregateAsync(indexScan);
        public Task<bool> IsAggregateCompleteAsync(CatalogIndexScan indexScan) => _adapter.IsAggregateCompleteAsync(indexScan);
        public Task FinalizeAsync(CatalogIndexScan indexScan) => _adapter.FinalizeAsync(indexScan);
    }
}

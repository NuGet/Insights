using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Worker.CatalogLeafItemToCsv
{
    public class CatalogLeafItemToCsvDriver : ICatalogLeafScanNonBatchDriver, ICsvStorage<CatalogLeafItemRecord>
    {
        private readonly CatalogScanToCsvHelper<CatalogLeafItemRecord> _helper;
        private readonly CatalogClient _catalogClient;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;

        public CatalogLeafItemToCsvDriver(
            CatalogScanToCsvHelper<CatalogLeafItemRecord> helper,
            CatalogClient catalogClient,
            IOptions<ExplorePackagesWorkerSettings> options)
        {
            _helper = helper;
            _catalogClient = catalogClient;
            _options = options;
        }

        public string ResultsContainerName => _options.Value.CatalogLeafItemContainerName;

        public List<CatalogLeafItemRecord> Prune(List<CatalogLeafItemRecord> records)
        {
            return records
                .Distinct()
                .OrderBy(x => x.CommitTimestamp)
                .ThenBy(x => x.Identity, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task InitializeAsync(CatalogIndexScan indexScan)
        {
            await _helper.InitializeAsync(indexScan, ResultsContainerName);
        }

        public Task<CatalogIndexScanResult> ProcessIndexAsync(CatalogIndexScan indexScan)
        {
            return Task.FromResult(CatalogIndexScanResult.ExpandAllLeaves);
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
                    LowerId = x.PackageId.ToLowerInvariant(),
                    Identity = $"{x.PackageId}/{x.ParsePackageVersion().ToNormalizedString()}".ToLowerInvariant(),
                    Id = x.PackageId,
                    Version = x.PackageVersion,
                    Type = x.Type,
                    Url = x.Url,

                    PageUrl = pageScan.Url,
                })
                .ToList();
            await _helper.AppendAsync(
                pageScan.StorageSuffix,
                _options.Value.AppendResultStorageBucketCount,
                pageScan.Url,
                records);

            return CatalogPageScanResult.Processed;
        }

        public Task<DriverResult> ProcessLeafAsync(CatalogLeafScan leafScan)
        {
            throw new NotImplementedException();
        }

        public Task StartAggregateAsync(CatalogIndexScan indexScan)
        {
            return _helper.StartAggregateAsync(indexScan);
        }

        public Task<bool> IsAggregateCompleteAsync(CatalogIndexScan indexScan)
        {
            return _helper.IsAggregateCompleteAsync(indexScan);
        }

        public Task FinalizeAsync(CatalogIndexScan indexScan)
        {
            return _helper.FinalizeAsync(indexScan);
        }

        public Task StartCustomExpandAsync(CatalogIndexScan indexScan)
        {
            throw new NotImplementedException();
        }

        public Task<bool> IsCustomExpandCompleteAsync(CatalogIndexScan indexScan)
        {
            throw new NotImplementedException();
        }

        public Task<CatalogLeafItem> MakeReprocessItemOrNullAsync(CatalogLeafItemRecord record)
        {
            throw new NotImplementedException();
        }
    }
}

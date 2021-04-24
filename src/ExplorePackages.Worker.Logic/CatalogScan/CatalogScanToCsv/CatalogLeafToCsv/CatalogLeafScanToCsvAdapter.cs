using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogLeafScanToCsvAdapter<T> : ICatalogLeafScanNonBatchDriver where T : class, ICsvRecord
    {
        private readonly SchemaSerializer _schemaSerializer;
        private readonly CatalogScanToCsvHelper<T> _helper;
        private readonly ICsvStorage<T> _storage;
        private readonly ICatalogLeafToCsvDriver<T> _driver;
        private readonly IOptionsSnapshot<ExplorePackagesWorkerSettings> _options;

        public CatalogLeafScanToCsvAdapter(
            SchemaSerializer schemaSerializer,
            CatalogScanToCsvHelper<T> helper,
            ICsvStorage<T> storage,
            ICatalogLeafToCsvDriver<T> driver,
            IOptionsSnapshot<ExplorePackagesWorkerSettings> options)
        {
            _schemaSerializer = schemaSerializer;
            _helper = helper;
            _storage = storage;
            _driver = driver;
            _options = options;
        }

        public async Task InitializeAsync(CatalogIndexScan indexScan)
        {
            await _helper.InitializeAsync(indexScan, _storage.ResultsContainerName);
            await _driver.InitializeAsync();
        }

        public Task<CatalogIndexScanResult> ProcessIndexAsync(CatalogIndexScan indexScan)
        {
            var parameters = (CatalogLeafToCsvParameters)_schemaSerializer.Deserialize(indexScan.DriverParameters).Data;

            CatalogIndexScanResult result;
            switch (parameters.Mode)
            {
                case CatalogLeafToCsvMode.AllLeaves:
                    result = CatalogIndexScanResult.ExpandAllLeaves;
                    break;
                case CatalogLeafToCsvMode.LatestLeaves:
                    result = _driver.SingleMessagePerId ? CatalogIndexScanResult.ExpandLatestLeavesPerId : CatalogIndexScanResult.ExpandLatestLeaves;
                    break;
                case CatalogLeafToCsvMode.Reprocess:
                    result = CatalogIndexScanResult.CustomExpand;
                    break;
                default:
                    throw new NotImplementedException();
            }

            return Task.FromResult(result);
        }

        public Task StartCustomExpandAsync(CatalogIndexScan indexScan)
        {
            return _helper.StartCustomExpandAsync(indexScan, _storage.ResultsContainerName);
        }

        public Task<bool> IsCustomExpandCompleteAsync(CatalogIndexScan indexScan)
        {
            return _helper.IsCustomExpandCompleteAsync(indexScan);
        }

        public Task<CatalogPageScanResult> ProcessPageAsync(CatalogPageScan pageScan)
        {
            return Task.FromResult(CatalogPageScanResult.ExpandAllowDuplicates);
        }

        public async Task<DriverResult> ProcessLeafAsync(CatalogLeafScan leafScan)
        {
            var leafItem = leafScan.ToLeafItem();
            var result = await _driver.ProcessLeafAsync(leafItem, leafScan.AttemptCount);
            if (result.Type == DriverResultType.TryAgainLater)
            {
                return result;
            }

            if (!result.Value.Records.Any())
            {
                return result;
            }

            await _helper.AppendAsync(
                leafScan.StorageSuffix,
                _options.Value.AppendResultStorageBucketCount,
                result.Value.BucketKey,
                result.Value.Records);
            return result;
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
    }
}

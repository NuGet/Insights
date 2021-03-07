using System;
using System.Linq;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogLeafScanToCsvAdapter<T> : ICatalogLeafScanNonBatchDriver where T : ICsvRecord<T>, new()
    {
        private readonly SchemaSerializer _schemaSerializer;
        private readonly CatalogScanToCsvAdapter<T> _adapter;
        private readonly ICatalogLeafToCsvDriver<T> _driver;

        public CatalogLeafScanToCsvAdapter(
            SchemaSerializer schemaSerializer,
            CatalogScanToCsvAdapter<T> adapter,
            ICatalogLeafToCsvDriver<T> driver)
        {
            _schemaSerializer = schemaSerializer;
            _adapter = adapter;
            _driver = driver;
        }

        public async Task InitializeAsync(CatalogIndexScan indexScan)
        {
            await _adapter.InitializeAsync(indexScan, _driver.ResultsContainerName);
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
            return _adapter.StartCustomExpandAsync(indexScan, _driver.ResultsContainerName);
        }

        public Task<bool> IsCustomExpandCompleteAsync(CatalogIndexScan indexScan)
        {
            return _adapter.IsCustomExpandCompleteAsync(indexScan);
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

            if (!result.Value.Any())
            {
                return result;
            }

            var bucketKey = _driver.GetBucketKey(leafItem);
            var parameters = (CatalogLeafToCsvParameters)_schemaSerializer.Deserialize(leafScan.DriverParameters).Data;
            await _adapter.AppendAsync(leafScan.StorageSuffix, parameters.BucketCount, bucketKey, result.Value);
            return result;
        }

        public Task StartAggregateAsync(CatalogIndexScan indexScan)
        {
            return _adapter.StartAggregateAsync(indexScan);
        }

        public Task<bool> IsAggregateCompleteAsync(CatalogIndexScan indexScan)
        {
            return _adapter.IsAggregateCompleteAsync(indexScan);
        }

        public Task FinalizeAsync(CatalogIndexScan indexScan)
        {
            return _adapter.FinalizeAsync(indexScan);
        }
    }
}

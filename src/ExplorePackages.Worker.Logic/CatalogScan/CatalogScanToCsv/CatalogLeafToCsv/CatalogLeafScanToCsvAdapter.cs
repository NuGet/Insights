using System.Linq;
using System.Threading.Tasks;
using NuGet.Versioning;

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
            if (parameters.OnlyLatestLeaves)
            {
                result = _driver.SingleMessagePerId ? CatalogIndexScanResult.ExpandLatestLeavesPerId : CatalogIndexScanResult.ExpandLatestLeaves;
            }
            else
            {
                result = CatalogIndexScanResult.ExpandAllLeaves;
            }

            return Task.FromResult(result);
        }

        public Task<CatalogPageScanResult> ProcessPageAsync(CatalogPageScan pageScan)
        {
            return Task.FromResult(CatalogPageScanResult.ExpandAllowDuplicates);
        }

        public async Task<DriverResult> ProcessLeafAsync(CatalogLeafScan leafScan)
        {
            var leafItem = leafScan.GetLeafItem();
            var result = await _driver.ProcessLeafAsync(leafItem);
            if (result.Type == DriverResultType.TryAgainLater)
            {
                return result;
            }

            if (!result.Value.Any())
            {
                return result;
            }

            var bucketKey = $"{leafScan.PackageId}/{NuGetVersion.Parse(leafScan.PackageVersion).ToNormalizedString()}".ToLowerInvariant();
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

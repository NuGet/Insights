using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Worker.EnqueueCatalogLeafScan
{
    public class EnqueueCatalogLeafScansDriver : ITableScanDriver<CatalogLeafScan>
    {
        private static readonly IList<string> _selectColumns = new[]
        {
            StorageUtility.PartitionKey,
            StorageUtility.RowKey, // this is the LeafId
            nameof(CatalogLeafScan.StorageSuffix),
            nameof(CatalogLeafScan.ScanId),
            nameof(CatalogLeafScan.PageId),
        };

        private readonly CatalogScanExpandService _expandService;

        public EnqueueCatalogLeafScansDriver(CatalogScanExpandService expandService)
        {
            _expandService = expandService;
        }

        public IList<string> SelectColumns => _selectColumns;

        public Task InitializeAsync(JToken parameters)
        {
            return Task.CompletedTask;
        }

        public async Task ProcessEntitySegmentAsync(string tableName, JToken parameters, List<CatalogLeafScan> entities)
        {
            await _expandService.EnqueueLeafScansAsync(entities);
        }
    }
}

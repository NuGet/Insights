using System;
using System.Collections.Generic;
using System.Linq;
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
            nameof(CatalogLeafScan.PackageId),
        };

        private readonly SchemaSerializer _serializer;
        private readonly CatalogScanExpandService _expandService;

        public EnqueueCatalogLeafScansDriver(
            SchemaSerializer schemaSerializer,
            CatalogScanExpandService expandService)
        {
            _serializer = schemaSerializer;
            _expandService = expandService;
        }

        public IList<string> SelectColumns => _selectColumns;

        public Task InitializeAsync(JToken parameters)
        {
            return Task.CompletedTask;
        }

        public async Task ProcessEntitySegmentAsync(string tableName, JToken parameters, IReadOnlyList<CatalogLeafScan> entities)
        {
            var deserializedParameters = (EnqueueCatalogLeafScansParameters)_serializer.Deserialize(parameters).Data;

            if (deserializedParameters.OneMessagePerId)
            {
                entities = entities
                    .GroupBy(x => x.PackageId, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();
            }

            await _expandService.EnqueueLeafScansAsync(entities);
        }
    }
}

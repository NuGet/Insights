using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Worker.FindLatestLeaves;
using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Worker.LatestLeafToLeafScan
{
    public class LatestLeafToLeafScanDriver : ITableScanDriver<LatestPackageLeaf>
    {
        private readonly SchemaSerializer _serializer;
        private readonly CatalogScanStorageService _storageService;
        private readonly CatalogScanExpandService _expandService;


        public LatestLeafToLeafScanDriver(
            SchemaSerializer serializer,
            CatalogScanStorageService storageService,
            CatalogScanExpandService expandService)
        {
            _serializer = serializer;
            _storageService = storageService;
            _expandService = expandService;
        }

        public IList<string> SelectColumns => null;

        public Task InitializeAsync(JToken parameters) => Task.CompletedTask;

        public async Task ProcessEntitySegmentAsync(string tableName, JToken parameters, List<LatestPackageLeaf> entities)
        {
            var indexScanMessage = (CatalogIndexScanMessage)_serializer.Deserialize(parameters).Data;
            var indexScan = await _storageService.GetIndexScanAsync(indexScanMessage.CursorName, indexScanMessage.ScanId);

            foreach (var pageGroup in entities.GroupBy(x => x.PageUrl))
            {
                // Convert the latest package leaf row to a leaf scan row
                var pageRank = pageGroup.Select(x => x.PageRank).Distinct().Single();
                var pageScan = _expandService.CreatePageScan(indexScan, pageGroup.Key, pageRank);

                var leafItemToRank = pageGroup
                    .Select(x => new
                    {
                        x.LeafRank,
                        Item = new CatalogLeafItem
                        {
                            CommitId = x.CommitId,
                            CommitTimestamp = x.CommitTimestamp,
                            PackageId = x.PackageId,
                            PackageVersion = x.PackageVersion,
                            Type = x.ParsedLeafType,
                            Url = x.Url,
                        },
                    })
                    .ToDictionary(x => x.Item, x => x.LeafRank);
                var leafItems = leafItemToRank
                    .OrderBy(x => x.Value)
                    .Select(x => x.Key)
                    .ToList();
                var leafScans = _expandService.CreateLeafScans(pageScan, leafItems, leafItemToRank);

                // Insert the leaf scan row
                await _expandService.InsertLeafScansAsync(pageScan, leafScans);

                // Enqueue the leaf scan
                await _expandService.EnqueueLeafScansAsync(leafScans);
            }
        }
    }
}

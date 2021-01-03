using System;
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

            // The "page scan" in this case is a set of entities, grouped by package ID. A page scan entity will not be
            // used at all.
            foreach (var packageIdGroup in entities.GroupBy(x => x.PackageId, StringComparer.OrdinalIgnoreCase))
            {
                var pageId = packageIdGroup.Key.ToLowerInvariant();

                var leafScans = packageIdGroup
                    .Select(x => new CatalogLeafScan(indexScan.StorageSuffix, indexScan.ScanId, pageId, x.LowerVersion)
                    {
                        ParsedDriverType = indexScan.ParsedDriverType,
                        DriverParameters = indexScan.DriverParameters,
                        Url = x.Url,
                        ParsedLeafType = x.ParsedLeafType,
                        CommitId = x.CommitId,
                        CommitTimestamp = x.CommitTimestamp,
                        PackageId = x.PackageId,
                        PackageVersion = x.PackageVersion,
                    })
                    .ToList();

                // Insert the leaf scan rows
                await _expandService.InsertLeafScansAsync(indexScan.StorageSuffix, indexScan.ScanId, pageId, leafScans, allowExtra: true);

                // Enqueue the leaf scans
                await _expandService.EnqueueLeafScansAsync(leafScans);
            }
        }
    }
}

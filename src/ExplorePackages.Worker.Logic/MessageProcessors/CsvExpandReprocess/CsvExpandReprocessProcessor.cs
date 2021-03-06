using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Worker
{
    public class CsvExpandReprocessProcessor<T> : IMessageProcessor<CsvExpandReprocessMessage<T>> where T : ICsvRecord<T>, new()
    {
        private readonly AppendResultStorageService _appendResultStorageService;
        private readonly TaskStateStorageService _taskStateStorageService;
        private readonly CatalogScanStorageService _catalogScanStorageService;
        private readonly ICatalogLeafToCsvDriver<T> _driver;
        private readonly ILogger<CsvExpandReprocessProcessor<T>> _logger;

        public CsvExpandReprocessProcessor(
            AppendResultStorageService appendResultStorageService,
            TaskStateStorageService taskStateStorageService,
            CatalogScanStorageService catalogScanStorageService,
            ICatalogLeafToCsvDriver<T> compactor,
            ILogger<CsvExpandReprocessProcessor<T>> logger)
        {
            _appendResultStorageService = appendResultStorageService;
            _taskStateStorageService = taskStateStorageService;
            _catalogScanStorageService = catalogScanStorageService;
            _driver = compactor;
            _logger = logger;
        }

        public async Task ProcessAsync(CsvExpandReprocessMessage<T> message, int dequeueCount)
        {
            var taskState = await _taskStateStorageService.GetAsync(message.TaskStateKey);
            if (taskState == null)
            {
                _logger.LogWarning("No matching task state was found.");
                return;
            }

            var indexScan = await _catalogScanStorageService.GetIndexScanAsync(message.CursorName, message.ScanId);
            if (indexScan == null)
            {
                _logger.LogWarning("No matching index scan was found.");
                return;
            }

            var records = await _appendResultStorageService.ReadAsync<T>(_driver.ResultsContainerName, message.Bucket);

            var items = new List<CatalogLeafItem>();
            foreach (var record in records)
            {
                var item = await _driver.MakeReprocessItemOrNullAsync(record);
                if (item != null)
                {
                    items.Add(item);
                }
            }

            var reprocessLeaves = items
                .Select(x => new CatalogLeafScan(indexScan.StorageSuffix, indexScan.ScanId, GetPageId(x.PackageId), GetLeafId(x.PackageVersion))
                {
                    ParsedDriverType = indexScan.ParsedDriverType,
                    DriverParameters = indexScan.DriverParameters,
                    Url = x.Url,
                    ParsedLeafType = x.Type,
                    CommitId = x.CommitId,
                    CommitTimestamp = x.CommitTimestamp,
                    PackageId = x.PackageId,
                    PackageVersion = x.PackageVersion,
                })
                .GroupBy(x => x.Url)
                .Select(g => g.OrderByDescending(x => x.CommitTimestamp).First());

            foreach (var leafScans in reprocessLeaves.GroupBy(x => new { x.StorageSuffix, x.ScanId, x.PageId }))
            {
                var createdLeaves = await _catalogScanStorageService.GetLeafScansAsync(leafScans.Key.StorageSuffix, leafScans.Key.ScanId, leafScans.Key.PageId);

                var allUrls = leafScans.Select(x => x.Url).ToHashSet();
                var createdUrls = createdLeaves.Select(x => x.Url).ToHashSet();
                var uncreatedUrls = allUrls.Except(createdUrls).ToHashSet();

                var uncreatedLeafScans = leafScans
                    .Where(x => uncreatedUrls.Contains(x.Url))
                    .ToList();
                await _catalogScanStorageService.InsertAsync(uncreatedLeafScans);
            }

            await _taskStateStorageService.DeleteAsync(taskState);
        }

        private static string GetPageId(string packageId)
        {
            return packageId.ToLowerInvariant();
        }

        private static string GetLeafId(string packageVersion)
        {
            return NuGetVersion.Parse(packageVersion).ToNormalizedString().ToLowerInvariant();
        }
    }
}

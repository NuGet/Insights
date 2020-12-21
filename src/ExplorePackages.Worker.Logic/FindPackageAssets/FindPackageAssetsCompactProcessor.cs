using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Worker.FindPackageAssets
{
    public class FindPackageAssetsCompactProcessor : IMessageProcessor<FindPackageAssetsCompactMessage>
    {
        private readonly AppendResultStorageService _storageService;
        private readonly TaskStateStorageService _taskStateStorageService;
        private readonly ICsvReader _csvReader;
        private readonly ILogger<FindPackageAssetsCompactProcessor> _logger;

        public FindPackageAssetsCompactProcessor(
            AppendResultStorageService storageService,
            TaskStateStorageService taskStateStorageService,
            ICsvReader csvReader,
            ILogger<FindPackageAssetsCompactProcessor> logger)
        {
            _storageService = storageService;
            _taskStateStorageService = taskStateStorageService;
            _csvReader = csvReader;
            _logger = logger;
        }

        public async Task ProcessAsync(FindPackageAssetsCompactMessage message)
        {
            TaskState taskState;
            if (message.Force
                && message.TaskStatePartitionKey == null
                && message.TaskStateRowKey == null
                && message.TaskStateStorageSuffix == null)
            {
                taskState = null;
            }
            else
            {
                taskState = await _taskStateStorageService.GetAsync(
                    message.TaskStateStorageSuffix,
                    message.TaskStatePartitionKey,
                    message.TaskStateRowKey);
            }

            if (!message.Force && taskState == null)
            {
                _logger.LogWarning("No matching task state was found.");
                return;
            }

            await _storageService.CompactAsync<PackageAsset>(
                message.SourceContainer,
                message.DestinationContainer,
                message.Bucket,
                force: message.Force,
                mergeExisting: true,
                PruneAssets,
                _csvReader);

            if (taskState != null)
            {
                await _taskStateStorageService.DeleteAsync(taskState);
            }
        }

        public static List<PackageAsset> PruneAssets(List<PackageAsset> allAssets)
        {
            return allAssets
                .GroupBy(x => x, PackageAssetIdVersionComparer.Instance) // Group by unique package version
                .Select(g => g
                    .GroupBy(x => new { x.ScanId, x.CatalogCommitTimestamp }) // Group package version assets by scan and catalog commit timestamp
                    .OrderByDescending(x => x.Key.CatalogCommitTimestamp)
                    .OrderByDescending(x => x.First().ScanTimestamp)
                    .First())
                .SelectMany(g => g)
                .OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Version, StringComparer.OrdinalIgnoreCase)
                .Distinct()
                .ToList();
        }
    }
}

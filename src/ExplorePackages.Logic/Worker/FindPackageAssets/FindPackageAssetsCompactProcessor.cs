using Knapcode.ExplorePackages.Logic.Worker.BlobStorage;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic.Worker.FindPackageAssets
{
    public class FindPackageAssetsCompactProcessor : IMessageProcessor<FindPackageAssetsCompactMessage>
    {
        private readonly AppendResultStorageService _storageService;
        private readonly TaskStateStorageService _taskStateStorageService;
        private readonly ILogger<FindPackageAssetsCompactProcessor> _logger;

        public FindPackageAssetsCompactProcessor(
            AppendResultStorageService storageService,
            TaskStateStorageService taskStateStorageService,
            ILogger<FindPackageAssetsCompactProcessor> logger)
        {
            _storageService = storageService;
            _taskStateStorageService = taskStateStorageService;
            _logger = logger;
        }

        public async Task ProcessAsync(FindPackageAssetsCompactMessage message)
        {
            var taskState = await _taskStateStorageService.GetAsync(
                message.TaskStateStorageSuffix,
                message.TaskStatePartitionKey,
                message.TaskStateRowKey);
            if (taskState == null)
            {
                _logger.LogWarning("No matching task state was found.");
                return;
            }

            await _storageService.CompactAsync<PackageAsset>(
                message.SourceContainer,
                message.DestinationContainer,
                message.Bucket,
                mergeExisting: true,
                PruneAssets);

            await _taskStateStorageService.DeleteAsync(taskState);
        }
        
        private static IEnumerable<PackageAsset> PruneAssets(IEnumerable<PackageAsset> allAssets)
        {
            return allAssets
                .GroupBy(x => new { Id = x.Id.ToLowerInvariant(), Version = x.Version.ToLowerInvariant() }) // Group by unique package version
                .Select(g => g
                    .GroupBy(x => x.ScanId) // Group package version assets by scan
                    .OrderByDescending(x => x.First().ScanTimestamp) // Ignore all but the most recent scan
                    .First())
                .SelectMany(g => g)
                .OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Version, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}

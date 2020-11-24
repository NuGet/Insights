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
                PruneAssets);

            if (taskState != null)
            {
                await _taskStateStorageService.DeleteAsync(taskState);
            }
        }
        
        private static IEnumerable<PackageAsset> PruneAssets(IEnumerable<PackageAsset> allAssets)
        {
            return allAssets
                .GroupBy(x => new { Id = x.Id.ToLowerInvariant(), Version = x.Version.ToLowerInvariant() }) // Group by unique package version
                .Select(g => g
                    .GroupBy(x => x.ScanId) // Group package version assets by scan
                    .OrderByDescending(x => x.First().Created) // Ignore all but the most recent scan of the most recent version of the package
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

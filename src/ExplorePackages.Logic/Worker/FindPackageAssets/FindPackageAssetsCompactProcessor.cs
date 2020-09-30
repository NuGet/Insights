using Knapcode.ExplorePackages.Logic.Worker.BlobStorage;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic.Worker.FindPackageAssets
{
    public class FindPackageAssetsCompactProcessor : IMessageProcessor<FindPackageAssetsCompactMessage>
    {
        private readonly AppendResultStorageService _storageService;

        public FindPackageAssetsCompactProcessor(AppendResultStorageService storageService)
        {
            _storageService = storageService;
        }

        public async Task ProcessAsync(FindPackageAssetsCompactMessage message)
        {
            await _storageService.CompactAsync<PackageAsset>(FindPackageAssetsConstants.ContainerName, message.Bucket, PruneAssets);
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
                .ToList();
        }
    }
}

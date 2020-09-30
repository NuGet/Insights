using Knapcode.ExplorePackages.Logic.Worker.BlobStorage;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic.Worker.RunRealRestore
{
    public class RunRealRestoreCompactProcessor : IMessageProcessor<RunRealRestoreCompactMessage>
    {
        private readonly AppendResultStorageService _storageService;

        public RunRealRestoreCompactProcessor(AppendResultStorageService storageService)
        {
            _storageService = storageService;
        }

        public async Task ProcessAsync(RunRealRestoreCompactMessage message)
        {
            await _storageService.CompactAsync<RealRestoreResult>(RunRealRestoreConstants.ContainerName, message.Bucket, PruneAssets);
        }
        
        private static IEnumerable<RealRestoreResult> PruneAssets(IEnumerable<RealRestoreResult> allAssets)
        {
            return allAssets
                .GroupBy(x => new { Id = x.Id.ToLowerInvariant(), Version = x.Version.ToLowerInvariant(), x.Framework, x.DotnetVersion })
                .Select(g => g
                    .OrderByDescending(x => x.Timestamp) // Ignore all but the most recent result
                    .First())
                .ToList();
        }
    }
}

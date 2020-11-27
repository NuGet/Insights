using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker.RunRealRestore
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
            await _storageService.CompactAsync<RealRestoreResult>(
                RunRealRestoreConstants.ContainerName,
                RunRealRestoreConstants.ContainerName,
                message.Bucket,
                force: true,
                mergeExisting: true,
                PruneAssets);
        }
        
        private static IEnumerable<RealRestoreResult> PruneAssets(IEnumerable<RealRestoreResult> allAssets)
        {
            return allAssets
                .GroupBy(x => new { Id = x.Id.ToLowerInvariant(), Version = x.Version.ToLowerInvariant(), x.Framework, x.DotnetVersion })
                .Select(g => g
                    .OrderByDescending(x => x.Timestamp) // Ignore all but the most recent result
                    .First())
                .OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x.Version, StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x.Framework, StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x.DotnetVersion, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}

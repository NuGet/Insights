using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Worker.RunRealRestore
{
    public class RunRealRestoreCompactProcessor : IMessageProcessor<RunRealRestoreCompactMessage>
    {
        private readonly AppendResultStorageService _storageService;
        private readonly IOptionsSnapshot<ExplorePackagesWorkerSettings> _options;

        public RunRealRestoreCompactProcessor(
            AppendResultStorageService storageService,
            IOptionsSnapshot<ExplorePackagesWorkerSettings> options)
        {
            _storageService = storageService;
            _options = options;
        }

        public async Task ProcessAsync(RunRealRestoreCompactMessage message)
        {
            await _storageService.CompactAsync<RealRestoreResult>(
                _options.Value.RunRealRestoreContainerName,
                _options.Value.RunRealRestoreContainerName,
                message.Bucket,
                force: true,
                mergeExisting: true,
                PruneAssets);
        }
        
        private static List<RealRestoreResult> PruneAssets(List<RealRestoreResult> allAssets)
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

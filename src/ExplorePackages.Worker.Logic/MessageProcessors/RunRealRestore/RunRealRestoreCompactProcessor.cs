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
        private readonly ICsvReader _csvReader;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;

        public RunRealRestoreCompactProcessor(
            AppendResultStorageService storageService,
            ICsvReader csvReader,
            IOptions<ExplorePackagesWorkerSettings> options)
        {
            _storageService = storageService;
            _csvReader = csvReader;
            _options = options;
        }

        public async Task ProcessAsync(RunRealRestoreCompactMessage message, long dequeueCount)
        {
            await _storageService.CompactAsync<RealRestoreResult>(
                _options.Value.RealRestoreContainerName,
                _options.Value.RealRestoreContainerName,
                message.Bucket,
                force: true,
                Prune);
        }

        private static List<RealRestoreResult> Prune(List<RealRestoreResult> allAssets)
        {
            return allAssets
                .GroupBy(x => new { Id = x.Id.ToLowerInvariant(), Version = x.Version.ToLowerInvariant(), x.Framework, x.DotnetVersion })
                .Select(g => g
                    .OrderByDescending(x => x.Timestamp) // Ignore all but the most recent result
                    .First())
                .OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Version, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Framework, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.DotnetVersion, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}

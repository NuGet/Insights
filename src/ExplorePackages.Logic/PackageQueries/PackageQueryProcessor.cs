using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageQueryProcessor
    {
        private readonly ILogger<PackageQueryProcessor> _logger;

        public PackageQueryProcessor(ILogger<PackageQueryProcessor> logger)
        {
            _logger = logger;
        }

        public async Task ConsumeWorkAsync(PackageQueryWork work, ConcurrentBag<PackageQueryResult> results)
        {
            foreach (var query in work.Queries)
            {
                var name = query.Name;
                var id = work.Context.Id;
                var version = work.Context.Version;

                if (query.NeedsMZip && work.Context.MZip == null)
                {
                    throw new InvalidOperationException("An .mzip is required to execute this query.");
                }

                if (query.NeedsNuspec && work.Context.Nuspec == null)
                {
                    throw new InvalidOperationException("An .nuspec is required to execute this query.");
                }

                var isMatch = false;
                try
                {
                    isMatch = await query.IsMatchAsync(work.Context, work.State);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Query failure {Name}: {Id} {Version}", name, id, version);
                    throw;
                }

                results.Add(new PackageQueryResult(
                    query,
                    new PackageIdentity(work.Context.Id, work.Context.Version),
                    isMatch));

                if (isMatch)
                {
                    _logger.LogInformation("Query match {Name}: {Id} {Version}", name, id, version);
                }
            }
        }
    }
}

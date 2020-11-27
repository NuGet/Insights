using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.Delta.Common;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Entities
{
    public class PackageQueryProcessor
    {
        private readonly PackageQueryService _queryService;
        private readonly PackageQueryContextBuilder _contextBuilder;
        private readonly ILogger<PackageQueryProcessor> _logger;

        public PackageQueryProcessor(
            PackageQueryService queryService,
            PackageQueryContextBuilder contextBuilder,
            ILogger<PackageQueryProcessor> logger)
        {
            _queryService = queryService;
            _contextBuilder = contextBuilder;
            _logger = logger;
        }

        public async Task EnqueueAsync(
            IProducerContext<PackageQueryWork> producer,
            IReadOnlyList<IPackageQuery> applicableQueries,
            PackageEntity package,
            bool includeNuspec,
            bool includeMZip,
            CancellationToken token)
        {
            var context = await _contextBuilder.GetPackageQueryContextFromDatabaseAsync(
                package,
                includeNuspec,
                includeMZip);

            var state = new PackageConsistencyState();

            await producer.EnqueueAsync(new PackageQueryWork(applicableQueries, context, state), token);
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

        public async Task PersistResults(ConcurrentBag<PackageQueryResult> results)
        {
            var queryGroups = results.GroupBy(x => x.Query);

            foreach (var queryGroup in queryGroups)
            {
                var query = queryGroup.Key;

                var resultGroups = queryGroup.ToLookup(
                    x => x.IsMatch,
                    x => x.PackageIdentity);

                if (resultGroups[true].Any())
                {
                    _logger.LogInformation("Adding new results for package query {QueryName}.", queryGroup.Key.Name);
                    await _queryService.AddQueryAsync(query.Name, query.CursorName);
                    await _queryService.AddMatchesAsync(query.Name, resultGroups[true].ToList());
                }

                if (resultGroups[false].Any())
                {
                    _logger.LogInformation("Removing existing results for package query {QueryName}.", queryGroup.Key.Name);
                    await _queryService.RemoveMatchesAsync(query.Name, resultGroups[false].ToList());
                }
            }
        }
    }
}

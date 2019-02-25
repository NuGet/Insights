using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.Delta.Common;
using Knapcode.ExplorePackages.Entities;
using Knapcode.ExplorePackages.Logic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageQueryCollector
    {
        private readonly PackageQueryProcessor _packageQueryProcessor;
        private readonly PackageQueryContextBuilder _contextBuilder;
        private readonly CursorService _cursorService;
        private readonly PackageCommitEnumerator _packageCommitEnumerator;
        private readonly PackageQueryService _queryService;
        private readonly IPackageService _packageService;
        private readonly IBatchSizeProvider _batchSizeProvider;
        private readonly ISingletonService _singletonService;
        private readonly ILogger<PackageQueryCollector> _logger;

        public PackageQueryCollector(
            PackageQueryProcessor packageQueryProcessor,
            PackageQueryContextBuilder contextBuilder,
            CursorService cursorService,
            PackageCommitEnumerator packageCommitEnumerator,
            PackageQueryService queryService,
            IPackageService packageService,
            IBatchSizeProvider batchSizeProvider,
            ISingletonService singletonService,
            ILogger<PackageQueryCollector> logger)
        {
            _packageQueryProcessor = packageQueryProcessor;
            _contextBuilder = contextBuilder;
            _cursorService = cursorService;
            _packageCommitEnumerator = packageCommitEnumerator;
            _queryService = queryService;
            _packageService = packageService;
            _batchSizeProvider = batchSizeProvider;
            _singletonService = singletonService;
            _logger = logger;
        }

        public async Task ProcessPackageAsync(IReadOnlyList<IPackageQuery> queries, IReadOnlyList<PackageIdentity> identities, CancellationToken token)
        {
            var results = new ConcurrentBag<PackageQueryResult>();

            var taskQueue = new TaskQueue<PackageQueryWork>(
                workerCount: 32,
                produceAsync: (p, t) => ProduceAsync(p, queries, identities, t),
                consumeAsync: (w, t) => _packageQueryProcessor.ConsumeWorkAsync(w, results),
                logger: _logger);

            await taskQueue.RunAsync();

            await PersistResults(results);
        }

        public async Task ProcessAsync(IReadOnlyList<IPackageQuery> queries, bool reprocess, CancellationToken token)
        {
            var bounds = await GetBoundsAsync(queries, reprocess);
            var complete = 0;
            var stopwatch = Stopwatch.StartNew();

            int commitCount;
            do
            {
                await _singletonService.RenewAsync();

                _logger.LogInformation("Using bounds between {Min:O} and {Max:O}.", bounds.Start, bounds.End);

                var commits = await GetCommitsAsync(bounds, reprocess);
                var packageCount = commits.Sum(x => x.Entities.Count);
                commitCount = commits.Count;

                if (commits.Any())
                {
                    var min = commits.Min(x => x.CommitTimestamp);
                    var max = commits.Max(x => x.CommitTimestamp);
                    bounds.Start = max;
                    _logger.LogInformation(
                        "Fetched {CommitCount} commits ({PackageCount} packages) between {Min:O} and {Max:O}.",
                        commitCount,
                        packageCount,
                        min,
                        max);
                }
                else
                {
                    _logger.LogInformation("No more commits were found within the bounds.");
                }

                var results = await ProcessCommitsAsync(queries, bounds, commits);

                complete += packageCount;
                _logger.LogInformation(
                    "{CompleteCount} completed ({PerSecond} per second).",
                    complete,
                    Math.Round(complete / stopwatch.Elapsed.TotalSeconds));

                await PersistResults(results);

                var queriesWithResults = results
                    .Select(x => x.Query)
                    .Distinct()
                    .ToList();

                await PersistCursorsAsync(bounds, queriesWithResults);
            }
            while (commitCount > 0);
        }

        private async Task ProduceAsync(
            IProducerContext<PackageQueryWork> producer,
            IReadOnlyList<IPackageQuery> queries,
            IReadOnlyList<PackageIdentity> identities,
            CancellationToken token)
        {
            var includeNuspec = queries.Any(x => x.NeedsNuspec);
            var includeMZip = queries.Any(x => x.NeedsMZip);

            foreach (var identity in identities)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                var package = await _packageService.GetPackageOrNullAsync(identity.Id, identity.Version);
                if (package == null)
                {
                    _logger.LogWarning("Package {Id} {Version} does not exist.", identity.Id, identity.Version);
                    continue;
                }

                await EnqueueAsync(
                    producer,
                    queries,
                    package,
                    includeNuspec,
                    includeMZip,
                    token);
            }
        }

        private async Task<ConcurrentBag<PackageQueryResult>> ProcessCommitsAsync(
            IReadOnlyList<IPackageQuery> queries,
            Bounds bounds,
            IReadOnlyList<EntityCommit<PackageEntity>> commits)
        {
            var results = new ConcurrentBag<PackageQueryResult>();

            var taskQueue = new TaskQueue<PackageQueryWork>(
                workerCount: 32,
                produceAsync: (p, t) => ProduceWorkAsync(p, queries, bounds, commits, t),
                consumeAsync: (w, t) => _packageQueryProcessor.ConsumeWorkAsync(w, results),
                logger: _logger);

            await taskQueue.RunAsync();

            return results;
        }

        private async Task<Bounds> GetBoundsAsync(IReadOnlyList<IPackageQuery> queries, bool reprocess)
        {
            Dictionary<string, string> queryNameToCursorName;
            IReadOnlyList<string> dependentCursorNames;

            if (!reprocess)
            {
                queryNameToCursorName = queries.ToDictionary(x => x.Name, x => x.CursorName);

                dependentCursorNames = new[]
                {
                    CursorNames.CatalogToDatabase,
                    CursorNames.CatalogToNuspecs,
                    CursorNames.MZipToDatabase,
                    CursorNames.NuGetOrg.FlatContainer,
                    CursorNames.NuGetOrg.Registration,
                    CursorNames.NuGetOrg.Search,
                };
            }
            else
            {
                queryNameToCursorName = queries.ToDictionary(x => x.Name, x => CursorNames.ReprocessPackageQueries);

                dependentCursorNames = queries
                    .Select(x => x.CursorName)
                    .ToList();
            }

            var cursorNameToStart = new Dictionary<string, DateTimeOffset>();
            foreach (var query in queries)
            {
                var cursorName = queryNameToCursorName[query.Name];
                if (!cursorNameToStart.ContainsKey(cursorName))
                {
                    var cursorStart = await _cursorService.GetValueAsync(cursorName);
                    cursorNameToStart[cursorName] = cursorStart;
                }
            }

            var end = await _cursorService.GetMinimumAsync(dependentCursorNames);

            return new Bounds(queryNameToCursorName, cursorNameToStart, end);
        }

        private async Task<IReadOnlyList<EntityCommit<PackageEntity>>> GetCommitsAsync(Bounds bounds, bool reprocess)
        {
            if (!reprocess)
            {
                return await _packageCommitEnumerator.GetCommitsAsync(
                    e => e
                        .Packages
                        .Include(x => x.V2Package),
                    bounds.Start,
                    bounds.End,
                    _batchSizeProvider.Get(BatchSizeType.PackageQueries));
            }
            else
            {
                var queryNames = bounds.QueryNameToCursorName.Keys.ToList();
                return await _packageCommitEnumerator.GetCommitsAsync(
                    e => e
                        .Packages
                        .Include(x => x.V2Package)
                        .Where(x => x
                            .PackageQueryMatches
                            .Any(pqm => queryNames.Contains(pqm.PackageQuery.Name))),
                    bounds.Start,
                    bounds.End,
                    _batchSizeProvider.Get(BatchSizeType.PackageQueries));
            }
        }

        private async Task ProduceWorkAsync(
            IProducerContext<PackageQueryWork> producer,
            IReadOnlyList<IPackageQuery> queries,
            Bounds bounds,
            IReadOnlyList<EntityCommit<PackageEntity>> commits,
            CancellationToken token)
        {
            await TaskProcessor.ExecuteAsync(
                commits.SelectMany(c => c.Entities.Select(p => new { Commit = c, Package = p })),
                async p =>
                {
                    var applicableQueries = queries
                        .Where(x => p.Commit.CommitTimestamp > bounds.CursorNameToStart[bounds.QueryNameToCursorName[x.Name]])
                        .ToList();

                    var includeNuspec = applicableQueries.Any(x => x.NeedsNuspec);
                    var includeMZip = applicableQueries.Any(x => x.NeedsMZip);
                    var package = p.Package;

                    await EnqueueAsync(producer, applicableQueries, package, includeNuspec, includeMZip, token);

                    return 0;
                },
                workerCount: 1,
                token: token);
        }

        private async Task EnqueueAsync(
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

        private async Task PersistCursorsAsync(Bounds bounds, IReadOnlyList<IPackageQuery> queries)
        {
            var cursorNameToQueryNames = bounds
                .QueryNameToCursorName
                .ToLookup(x => x.Value, x => x.Key)
                .ToDictionary(x => x.Key, x => new HashSet<string>(x));

            var cursorNames = new List<string>();
            foreach (var query in queries)
            {
                var cursorName = bounds.QueryNameToCursorName[query.Name];
                cursorNameToQueryNames[cursorName].Remove(query.Name);

                if (!cursorNameToQueryNames[cursorName].Any()
                    && bounds.CursorNameToStart[cursorName] < bounds.Start)
                {
                    _logger.LogInformation("Cursor {CursorName} moving to {Start:O}.", cursorName, bounds.Start);
                    cursorNames.Add(cursorName);
                    bounds.CursorNameToStart[cursorName] = bounds.Start;
                }
            }

            await _cursorService.SetValuesAsync(cursorNames, bounds.Start);
        }

        private async Task PersistResults(ConcurrentBag<PackageQueryResult> results)
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

        private class Bounds
        {
            public Bounds(
                IReadOnlyDictionary<string, string> queryNameToCursorName,
                Dictionary<string, DateTimeOffset> cursorNameToStart,
                DateTimeOffset end)
            {
                QueryNameToCursorName = queryNameToCursorName;
                CursorNameToStart = cursorNameToStart;
                Start = cursorNameToStart.Values.Min();
                End = end;
            }

            public IReadOnlyDictionary<string, string> QueryNameToCursorName { get; }
            public Dictionary<string, DateTimeOffset> CursorNameToStart { get; }
            public DateTimeOffset Start { get; set; }
            public DateTimeOffset End { get; }
        }
    }
}

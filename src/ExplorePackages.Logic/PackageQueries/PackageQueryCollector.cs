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
    /// <summary>
    /// Executes package queries on catalog commits.
    /// </summary>
    public class PackageQueryCollector
    {
        private readonly PackageQueryProcessor _processor;
        private readonly CursorService _cursorService;
        private readonly PackageCatalogCommitEnumerator _packageCatalogCommitEnumerator;
        private readonly PackageV2CommitEnumerator _packageV2CommitEnumerator;
        private readonly IBatchSizeProvider _batchSizeProvider;
        private readonly ISingletonService _singletonService;
        private readonly ILogger<PackageQueryCollector> _logger;

        public PackageQueryCollector(
            PackageQueryProcessor packageQueryProcessor,
            CursorService cursorService,
            PackageCatalogCommitEnumerator packageCatalogCommitEnumerator,
            PackageV2CommitEnumerator packageV2CommitEnumerator,
            IBatchSizeProvider batchSizeProvider,
            ISingletonService singletonService,
            ILogger<PackageQueryCollector> logger)
        {
            _processor = packageQueryProcessor;
            _cursorService = cursorService;
            _packageCatalogCommitEnumerator = packageCatalogCommitEnumerator;
            _packageV2CommitEnumerator = packageV2CommitEnumerator;
            _batchSizeProvider = batchSizeProvider;
            _singletonService = singletonService;
            _logger = logger;
        }

        public async Task ProcessAsync(IReadOnlyList<IPackageQuery> queries, bool reprocess, CancellationToken token)
        {
            var isV2QueryToQueries = queries.ToLookup(x => x.IsV2Query);

            var catalogQueries = isV2QueryToQueries[false].ToList();
            if (catalogQueries.Any())
            {
                _logger.LogInformation("Running package queries against the catalog.");
                await ProcessAsync(_packageCatalogCommitEnumerator, catalogQueries, reprocess);
            }

            var v2Queries = isV2QueryToQueries[true].ToList();
            if (v2Queries.Any())
            {
                _logger.LogInformation("Running package queries against V2.");
                await ProcessAsync(_packageV2CommitEnumerator, v2Queries, reprocess);
            }
        }

        private async Task ProcessAsync(
            IPackageCommitEnumerator packageCommitEnumerator,
            IReadOnlyList<IPackageQuery> queries,
            bool reprocess)
        {
            var bounds = await GetBoundsAsync(queries, reprocess);
            var complete = 0;
            var stopwatch = Stopwatch.StartNew();

            int commitCount;
            do
            {
                await _singletonService.RenewAsync();

                _logger.LogInformation("Using bounds between {Min:O} and {Max:O}.", bounds.Start, bounds.End);

                var commits = await GetCommitsAsync(packageCommitEnumerator, bounds, reprocess);
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

                await _processor.PersistResults(results);

                var queriesWithResults = results
                    .Select(x => x.Query)
                    .Distinct()
                    .ToList();

                await PersistCursorsAsync(bounds, queriesWithResults);
            }
            while (commitCount > 0);
        }

        private async Task<ConcurrentBag<PackageQueryResult>> ProcessCommitsAsync(
            IReadOnlyList<IPackageQuery> queries,
            PackageQueryBounds bounds,
            IReadOnlyList<EntityCommit<PackageEntity>> commits)
        {
            var results = new ConcurrentBag<PackageQueryResult>();

            var taskQueue = new TaskQueue<PackageQueryWork>(
                workerCount: 32,
                produceAsync: (p, t) => ProduceWorkAsync(p, queries, bounds, commits, t),
                consumeAsync: (w, t) => _processor.ConsumeWorkAsync(w, results),
                logger: _logger);

            await taskQueue.RunAsync();

            return results;
        }

        private async Task<PackageQueryBounds> GetBoundsAsync(IReadOnlyList<IPackageQuery> queries, bool reprocess)
        {
            Dictionary<string, string> queryNameToCursorName;
            IReadOnlyList<string> dependentCursorNames;

            if (!reprocess)
            {
                queryNameToCursorName = queries.ToDictionary(x => x.Name, x => x.CursorName);

                dependentCursorNames = new[]
                {
                    CursorNames.CatalogToDatabase,
                    CursorNames.Nuspecs,
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

            var delay = TimeSpan.Zero;
            if (queries.Any())
            {
                delay = queries.Max(x => x.Delay);
            }

            end -= delay;

            return new PackageQueryBounds(queryNameToCursorName, cursorNameToStart, end);
        }

        private async Task<IReadOnlyList<EntityCommit<PackageEntity>>> GetCommitsAsync(
            IPackageCommitEnumerator packageCommitEnumerator,
            PackageQueryBounds bounds,
            bool reprocess)
        {
            if (!reprocess)
            {
                return await packageCommitEnumerator.GetCommitsAsync(
                    e => e
                        .Packages
                        .Include(x => x.V2Package)
                        .Include(x => x.CatalogPackage),
                    bounds.Start,
                    bounds.End,
                    _batchSizeProvider.Get(BatchSizeType.PackageQueries));
            }
            else
            {
                var queryNames = bounds.QueryNameToCursorName.Keys.ToList();
                return await packageCommitEnumerator.GetCommitsAsync(
                    e => e
                        .Packages
                        .Include(x => x.V2Package)
                        .Include(x => x.CatalogPackage)
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
            PackageQueryBounds bounds,
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

                    await _processor.EnqueueAsync(producer, applicableQueries, package, includeNuspec, includeMZip, token);

                    return 0;
                },
                workerCount: 1,
                token: token);
        }

        private async Task PersistCursorsAsync(PackageQueryBounds bounds, IReadOnlyList<IPackageQuery> queries)
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
    }
}

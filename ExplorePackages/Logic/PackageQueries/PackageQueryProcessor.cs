using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Knapcode.ExplorePackages.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageQueryProcessor
    {
        private readonly PackageQueryContextBuilder _contextBuilder;
        private readonly CursorService _cursorService;
        private readonly PackageCommitEnumerator _packageCommitEnumerator;
        private readonly PackageQueryService _queryService;
        private readonly IPackageService _packageService;
        private readonly ILogger<PackageQueryProcessor> _logger;

        public PackageQueryProcessor(
            PackageQueryContextBuilder contextBuilder,
            CursorService cursorService,
            PackageCommitEnumerator packageCommitEnumerator,
            PackageQueryService queryService,
            IPackageService packageService,
            ILogger<PackageQueryProcessor> logger)
        {
            _contextBuilder = contextBuilder;
            _cursorService = cursorService;
            _packageCommitEnumerator = packageCommitEnumerator;
            _queryService = queryService;
            _packageService = packageService;
            _logger = logger;
        }

        public async Task ProcessPackageAsync(IReadOnlyList<IPackageQuery> queries, IReadOnlyList<PackageIdentity> identities, CancellationToken token)
        {
            var results = new ConcurrentBag<Result>();

            var taskQueue = new TaskQueue<Work>(
                workerCount: 32,
                workAsync: w => ConsumeWorkAsync(w, results));

            taskQueue.Start();

            foreach (var identity in identities)
            {
                var package = await _packageService.GetPackageOrNullAsync(identity.Id, identity.Version);

                if (package == null)
                {
                    _logger.LogWarning("Package {Id} {Version} does not exist.", identity.Id, identity.Version);
                    continue;
                }

                var context = _contextBuilder.GetPackageQueryFromDatabasePackageContext(package);
                var state = new PackageConsistencyState();
                var work = new Work(queries, context, state);
                taskQueue.Enqueue(work);
            }

            await taskQueue.CompleteAsync();

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

                await PersistResultsAndCursorsAsync(bounds, results);
            }
            while (commitCount > 0);
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
                    5000);
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
                    5000);
            }
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

    private async Task<ConcurrentBag<Result>> ProcessCommitsAsync(
            IReadOnlyList<IPackageQuery> queries,
            Bounds bounds,
            IReadOnlyList<EntityCommit<PackageEntity>> commits)
        {
            var results = new ConcurrentBag<Result>();

            var taskQueue = new TaskQueue<Work>(
                workerCount: 32,
                workAsync: w => ConsumeWorkAsync(w, results));

            taskQueue.Start();
            ProduceWork(queries, bounds, commits, taskQueue);
            await taskQueue.CompleteAsync();

            return results;
        }

        private void ProduceWork(
            IReadOnlyList<IPackageQuery> queries,
            Bounds bounds,
            IReadOnlyList<EntityCommit<PackageEntity>> commits,
            TaskQueue<Work> taskQueue)
        {
            foreach (var commit in commits)
            {
                foreach (var package in commit.Entities)
                {
                    var applicableQueries = queries
                        .Where(x => commit.CommitTimestamp > bounds.CursorNameToStart[bounds.QueryNameToCursorName[x.Name]])
                        .ToList();

                    var context = _contextBuilder.GetPackageQueryFromDatabasePackageContext(package);
                    var state = new PackageConsistencyState();

                    taskQueue.Enqueue(new Work(applicableQueries, context, state));
                }
            }
        }

        private async Task ConsumeWorkAsync(Work work, ConcurrentBag<Result> results)
        {
            foreach (var query in work.Queries)
            {
                var name = query.Name;
                var id = work.Context.Package.Id;
                var version = work.Context.Package.Version;

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

                results.Add(new Result(
                    query,
                    new PackageIdentity(work.Context.Package.Id, work.Context.Package.Version),
                    isMatch));

                if (isMatch)
                {
                    _logger.LogInformation("Query match {Name}: {Id} {Version}", name, id, version);
                }
            }
        }

        private async Task PersistResultsAndCursorsAsync(Bounds bounds, ConcurrentBag<Result> results)
        {
            await PersistResults(results);

            var queries = results
                .Select(x => x.Query)
                .Distinct()
                .ToList();

            await PersistCursorsAsync(bounds, queries);
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

        private async Task PersistResults(ConcurrentBag<Result> results)
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
                    await _queryService.AddQueryAsync(query.Name, query.CursorName);
                    await _queryService.AddMatchesAsync(query.Name, resultGroups[true].ToList());
                }

                if (resultGroups[false].Any())
                {
                    await _queryService.RemoveMatchesAsync(query.Name, resultGroups[false].ToList());
                }
            }
        }

        private async Task PersistResults(IEnumerable<IGrouping<IPackageQuery, Result>> queryGroups)
        {
            foreach (var queryGroup in queryGroups)
            {
                var query = queryGroup.Key;

                var resultGroups = queryGroup.ToLookup(
                    x => x.IsMatch,
                    x => x.PackageIdentity);

                if (resultGroups[true].Any())
                {
                    await _queryService.AddQueryAsync(query.Name, query.CursorName);
                    await _queryService.AddMatchesAsync(query.Name, resultGroups[true].ToList());
                }

                if (resultGroups[false].Any())
                {
                    await _queryService.RemoveMatchesAsync(query.Name, resultGroups[false].ToList());
                }
            }
        }

        private class Work
        {
            public Work(IReadOnlyList<IPackageQuery> queries, PackageQueryContext context, PackageConsistencyState state)
            {
                Queries = queries;
                Context = context;
                State = state;
            }

            public IReadOnlyList<IPackageQuery> Queries { get; }
            public PackageQueryContext Context { get; }
            public PackageConsistencyState State { get; }
        }

        private class Result
        {
            public Result(IPackageQuery query, PackageIdentity packageIdentity, bool isMatch)
            {
                Query = query;
                PackageIdentity = packageIdentity;
                IsMatch = isMatch;
            }

            public IPackageQuery Query { get; }
            public PackageIdentity PackageIdentity { get; }
            public bool IsMatch { get; }
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

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
using NuGet.Common;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageQueryProcessor
    {
        private readonly PackageQueryContextBuilder _contextBuilder;
        private readonly CursorService _cursorService;
        private readonly PackageCommitEnumerator _packageCommitEnumerator;
        private readonly PackageQueryService _queryService;
        private readonly ILogger _log;

        public PackageQueryProcessor(
            PackageQueryContextBuilder contextBuilder,
            CursorService cursorService,
            PackageCommitEnumerator packageCommitEnumerator,
            PackageQueryService queryService,
            ILogger log)
        {
            _contextBuilder = contextBuilder;
            _cursorService = cursorService;
            _packageCommitEnumerator = packageCommitEnumerator;
            _queryService = queryService;
            _log = log;
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
                    _log.LogInformation($"Fetched {commits.Count} commits ({packageCount} packages) between {min:O} and {max:O}.");
                }
                else
                {
                    _log.LogInformation("No more commits were found within the bounds.");
                }

                var results = await ProcessCommitsAsync(queries, bounds, commits);

                complete += packageCount;
                _log.LogInformation($"{complete} completed ({Math.Round(complete / stopwatch.Elapsed.TotalSeconds)} per second).");

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
                catch (Exception e)
                {
                    _log.LogError($"Query failure {name}: {id} {version}"
                        + Environment.NewLine
                        + "  "
                        + e.Message);
                    throw;
                }

                results.Add(new Result(
                    query,
                    new PackageIdentity(work.Context.Package.Id, work.Context.Package.Version),
                    isMatch));

                if (isMatch)
                {
                    _log.LogInformation($"Query match {name}: {id} {version}");
                }
            }
        }
        

        private async Task PersistResultsAndCursorsAsync(Bounds bounds, ConcurrentBag<Result> results)
        {
            var queryGroups = results.GroupBy(x => x.Query);
            var cursorNameToQueryNames = bounds
                .QueryNameToCursorName
                .ToLookup(x => x.Value, x => x.Key)
                .ToDictionary(x => x.Key, x => new HashSet<string>(x));

            var cursorNames = new List<string>();
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

                var cursorName = bounds.QueryNameToCursorName[query.Name];
                cursorNameToQueryNames[cursorName].Remove(query.Name);

                if (!cursorNameToQueryNames[cursorName].Any()
                    && bounds.CursorNameToStart[cursorName] < bounds.Start)
                {
                    _log.LogInformation($"Cursor {cursorName} moving to {bounds.Start:O}.");
                    cursorNames.Add(cursorName);
                    bounds.CursorNameToStart[cursorName] = bounds.Start;
                }
            }

            await _cursorService.SetValuesAsync(cursorNames, bounds.Start);
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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Support;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageQueryProcessor
    {
        private readonly PackageQueryContextBuilder _contextBuilder;
        private readonly CursorService _cursorService;
        private readonly PackageService _packageService;
        private readonly PackageQueryService _queryService;
        private readonly ILogger _log;

        public PackageQueryProcessor(
            PackageQueryContextBuilder contextBuilder,
            CursorService cursorService,
            PackageService packageService,
            PackageQueryService queryService,
            ILogger log)
        {
            _contextBuilder = contextBuilder;
            _cursorService = cursorService;
            _packageService = packageService;
            _queryService = queryService;
            _log = log;
        }

        public async Task ProcessAsync(IReadOnlyList<IPackageQuery> queries, CancellationToken token)
        {
            var bounds = await GetBoundsAsync(queries);
            var complete = 0;
            var stopwatch = Stopwatch.StartNew();

            int commitCount;
            do
            {
                var commits = await _packageService.GetPackageCommitsAsync(bounds.Start, bounds.End);
                commitCount = commits.Count;

                if (commits.Any())
                {
                    bounds.Start = commits.Max(x => x.CommitTimestamp);
                }

                var results = await ProcessCommitsAsync(queries, bounds.Starts, commits);

                complete += commits.Sum(x => x.Packages.Count);
                _log.LogInformation($"{complete} completed ({Math.Round(complete / stopwatch.Elapsed.TotalSeconds)} per second).");

                await PersistResultsAndCursorsAsync(bounds, results);
            }
            while (commitCount > 0);
        }

        private async Task<Bounds> GetBoundsAsync(IReadOnlyList<IPackageQuery> queries)
        {
            var starts = new Dictionary<string, DateTimeOffset>();
            
            foreach (var query in queries)
            {
                if (!starts.ContainsKey(query.CursorName))
                {
                    var cursorStart = await _cursorService.GetAsync(query.CursorName);
                    starts[query.CursorName] = cursorStart;
                }
            }

            var end = await _cursorService.GetMinimumAsync(new[]
            {
                CursorNames.CatalogToDatabase,
                CursorNames.CatalogToNuspecs,
                CursorNames.NuGetOrg.FlatContainer,
                CursorNames.NuGetOrg.Registration,
                CursorNames.NuGetOrg.Search,
            });

            return new Bounds(starts, end);
        }

    private async Task<ConcurrentBag<Result>> ProcessCommitsAsync(
            IReadOnlyList<IPackageQuery> queries,
            IReadOnlyDictionary<string, DateTimeOffset> cursorStarts,
            IReadOnlyList<PackageCommit> commits)
        {
            var results = new ConcurrentBag<Result>();

            var taskQueue = new TaskQueue<Work>(
                workerCount: 32,
                workAsync: w => ConsumeWorkAsync(w, results));

            taskQueue.Start();
            ProduceWork(queries, cursorStarts, commits, taskQueue);
            await taskQueue.CompleteAsync();

            return results;
        }

        private void ProduceWork(
            IReadOnlyList<IPackageQuery> queries,
            IReadOnlyDictionary<string, DateTimeOffset> cursorStarts,
            IReadOnlyList<PackageCommit> commits,
            TaskQueue<Work> taskQueue)
        {
            foreach (var commit in commits)
            {
                foreach (var package in commit.Packages)
                {
                    var applicableQueries = queries
                        .Where(x => commit.CommitTimestamp > cursorStarts[x.CursorName])
                        .ToList();

                    var context = _contextBuilder.GetPackageQueryFromDatabasePackageContext(package);
                    var state = new PackageConsistencyState();

                    taskQueue.Enqueue(new Work(queries, context, state));
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

                if (bounds.Starts[query.CursorName] < bounds.Start)
                {
                    _log.LogInformation($"Cursor {query.CursorName} moving to {bounds.Start:O}.");
                    await _cursorService.SetAsync(query.CursorName, bounds.Start);
                    bounds.Starts[query.CursorName] = bounds.Start;
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
            public Bounds(Dictionary<string, DateTimeOffset> starts, DateTimeOffset end)
            {
                Starts = starts;
                Start = starts.Values.Max();
                End = end;
            }

            public Dictionary<string, DateTimeOffset> Starts { get; }
            public DateTimeOffset Start { get; set; }
            public DateTimeOffset End { get; }
        }
    }
}

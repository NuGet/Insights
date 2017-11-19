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
        private readonly List<IPackageQuery> _queries;
        private readonly ILogger _log;

        public PackageQueryProcessor(
            PackageQueryContextBuilder contextBuilder,
            CursorService cursorService,
            PackageService packageService,
            PackageQueryService queryService,
            IEnumerable<IPackageQuery> queries,
            ILogger log)
        {
            _contextBuilder = contextBuilder;
            _cursorService = cursorService;
            _packageService = packageService;
            _queryService = queryService;
            _queries = queries.ToList();
            _log = log;
        }

        public async Task ProcessAsync(CancellationToken token)
        {
            var cursorStarts = new Dictionary<string, DateTimeOffset>();
            var start = await GetMinimumQueryStartAsync(cursorStarts);
            var end = await _cursorService.GetMinimumAsync(new[]
{
                CursorNames.CatalogToDatabase,
                CursorNames.CatalogToNuspecs,
                CursorNames.NuGetOrg.FlatContainer,
                CursorNames.NuGetOrg.Registration,
                CursorNames.NuGetOrg.Search,
            });

            var complete = 0;
            var stopwatch = Stopwatch.StartNew();

            int commitCount;
            do
            {
                var commits = await _packageService.GetPackageCommitsAsync(start, end);
                start = commits.Max(x => x.CommitTimestamp);
                commitCount = commits.Count;

                var results = await ProcessCommitsAsync(cursorStarts, commits);

                complete += commits.Sum(x => x.Packages.Count);
                _log.LogInformation($"{complete} completed ({Math.Round(complete / stopwatch.Elapsed.TotalSeconds)} per second).");

                await PersistResultsAndCursorsAsync(cursorStarts, start, results);
            }
            while (commitCount > 0);
        }

        private async Task<ConcurrentBag<Result>> ProcessCommitsAsync(
            IReadOnlyDictionary<string, DateTimeOffset> cursorStarts,
            IReadOnlyList<PackageCommit> commits)
        {
            var results = new ConcurrentBag<Result>();

            var taskQueue = new TaskQueue<Work>(
                workerCount: 32,
                workAsync: w => ConsumeWorkAsync(w, results));

            taskQueue.Start();
            ProduceWork(cursorStarts, commits, taskQueue);
            await taskQueue.CompleteAsync();

            return results;
        }

        private void ProduceWork(
            IReadOnlyDictionary<string, DateTimeOffset> cursorStarts,
            IReadOnlyList<PackageCommit> commits,
            TaskQueue<Work> taskQueue)
        {
            foreach (var commit in commits)
            {
                foreach (var package in commit.Packages)
                {
                    var queries = _queries
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

        private async Task<DateTimeOffset> GetMinimumQueryStartAsync(
            Dictionary<string, DateTimeOffset> cursorStarts)
        {
            var start = DateTimeOffset.MaxValue;
            foreach (var query in _queries)
            {
                if (!cursorStarts.ContainsKey(query.CursorName))
                {
                    var cursorStart = await _cursorService.GetAsync(query.CursorName);
                    cursorStarts[query.CursorName] = cursorStart;

                    if (cursorStart < start)
                    {
                        start = cursorStart;
                    }
                }
            }

            return start;
        }

        private async Task PersistResultsAndCursorsAsync(
            Dictionary<string, DateTimeOffset> cursorStarts,
            DateTimeOffset start,
            ConcurrentBag<Result> results)
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

                if (cursorStarts[query.CursorName] < start)
                {
                    _log.LogInformation($"Cursor {query.CursorName} moving to {start:O}.");
                    await _cursorService.SetAsync(query.CursorName, start);
                    cursorStarts[query.CursorName] = start;
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
    }
}

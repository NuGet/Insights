using System;
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
        private readonly PackageQueryService _queryService;
        private readonly List<IPackageQuery> _queries;
        private readonly ILogger _log;

        public PackageQueryProcessor(
            PackageQueryContextBuilder contextBuilder,
            IEnumerable<IPackageQuery> queries,
            ILogger log)
        {
            _contextBuilder = contextBuilder;
            _queryService = new PackageQueryService(log);
            _queries = queries.ToList();
            _log = log;
        }

        public async Task ProcessAsync(CancellationToken token)
        {
            var cursorService = new CursorService();

            var cursorStarts = new Dictionary<string, DateTimeOffset>();
            var start = await GetMinimumQueryStartAsync(cursorService, cursorStarts);
            var end = await cursorService.GetMinimumAsync(new[]
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
            var packageService = new PackageService(_log);

            do
            {
                var commits = await packageService.GetPackageCommitsAsync(start, end);
                commitCount = commits.Count;

                var allQueryMatchesLock = new object();
                var allQueryMatches = _queries.ToDictionary(
                    x => x.Name,
                    x => new List<PackageIdentity>());

                var taskQueue = new TaskQueue<Work>(
                    workerCount: 32,
                    workAsync: w => ConsumeWorkAsync(allQueryMatchesLock, allQueryMatches, w));

                taskQueue.Start();
                start = ProduceWork(cursorStarts, start, commits, taskQueue);
                await taskQueue.CompleteAsync();

                complete += commits.Sum(x => x.Packages.Count);
                _log.LogInformation($"{complete} completed ({Math.Round(complete / stopwatch.Elapsed.TotalSeconds)} per second).");

                await PersistResultsAndCursorsAsync(cursorService, cursorStarts, start, allQueryMatches);
            }
            while (commitCount > 0);
        }

        private DateTimeOffset ProduceWork(
            Dictionary<string, DateTimeOffset> cursorStarts,
            DateTimeOffset start,
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

                    start = commit.CommitTimestamp;
                }
            }

            return start;
        }

        private async Task ConsumeWorkAsync(object allQueryMatchesLock, Dictionary<string, List<PackageIdentity>> allQueryMatches, Work work)
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

                if (isMatch)
                {
                    _log.LogInformation($"Query match {name}: {id} {version}");
                    lock (allQueryMatchesLock)
                    {
                        allQueryMatches[name].Add(new PackageIdentity(id, version));
                    }
                }
            }
        }

        private async Task<DateTimeOffset> GetMinimumQueryStartAsync(
            CursorService cursorService,
            Dictionary<string, DateTimeOffset> cursorStarts)
        {
            var start = DateTimeOffset.MaxValue;
            foreach (var query in _queries)
            {
                if (!cursorStarts.ContainsKey(query.CursorName))
                {
                    var cursorStart = await cursorService.GetAsync(query.CursorName);
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
            CursorService cursorService,
            Dictionary<string, DateTimeOffset> cursorStarts,
            DateTimeOffset start,
            Dictionary<string, List<PackageIdentity>> allQueryMatches)
        {
            foreach (var query in _queries)
            {
                if (allQueryMatches[query.Name].Any())
                {
                    await _queryService.AddQueryAsync(query.Name, query.CursorName);
                    await _queryService.AddMatchesAsync(query.Name, allQueryMatches[query.Name]);
                }

                if (cursorStarts[query.CursorName] < start)
                {
                    _log.LogInformation($"Cursor {query.CursorName} moving to {start:O}.");
                    await cursorService.SetAsync(query.CursorName, start);
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
    }
}

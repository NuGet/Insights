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
    public class PackageCommitCollector
    {
        private readonly CursorService _cursorService;
        private readonly PackageService _packageService;
        private readonly ILogger _log;

        public PackageCommitCollector(
            CursorService cursorService,
            PackageService packageService,
            ILogger log)
        {
            _cursorService = cursorService;
            _packageService = packageService;
            _log = log;
        }
        
        public async Task ProcessAsync<T>(
            IPackageCommitProcessor<T> processor,
            ProcessMode processMode,
            CancellationToken token)
        {
            var start = await _cursorService.GetValueAsync(processor.CursorName);
            var end = await _cursorService.GetMinimumAsync(processor.DependencyCursorNames);

            int commitCount;
            do
            {
                var commits = await _packageService.GetPackageCommitsAsync(start, end);
                var packageCount = commits.Sum(x => x.Packages.Count);
                commitCount = commits.Count;

                if (commits.Any())
                {
                    var min = commits.Min(x => x.CommitTimestamp);
                    var max = commits.Max(x => x.CommitTimestamp);
                    start = max;
                    _log.LogInformation($"Fetched {commits.Count} commits ({packageCount} packages) between {min:O} and {max:O}.");
                }
                else
                {
                    _log.LogInformation("No more commits were found within the bounds.");
                }

                switch (processMode)
                {
                    case ProcessMode.Sequentially:
                        await ProcessSequentiallyAsync(processor, commits, token);
                        break;
                    case ProcessMode.TaskQueue:
                        await ProcessTaskQueueAsync(processor, commits, token);
                        break;
                    default:
                        throw new NotImplementedException();
                }

                if (commits.Any())
                {
                    _log.LogInformation($"Cursor {processor.CursorName} moving to {start:O}.");
                    await _cursorService.SetValueAsync(processor.CursorName, start);
                }
            }
            while (commitCount > 0);
        }

        private async Task ProcessTaskQueueAsync<T>(
            IPackageCommitProcessor<T> processor,
            IReadOnlyList<PackageCommit> commits,
            CancellationToken token)
        {
            var taskQueue = new TaskQueue<IReadOnlyList<T>>(
                workerCount: 32,
                workAsync: x => processor.ProcessBatchAsync(x));

            taskQueue.Start();

            foreach (var commit in commits)
            {
                foreach (var package in commit.Packages)
                {
                    var item = await processor.InitializeItemAsync(package, token);
                    if (item == null)
                    {
                        continue;
                    }

                    taskQueue.Enqueue(new List<T> { item });
                }
            }

            await taskQueue.CompleteAsync();
        }

        private async Task ProcessSequentiallyAsync<T>(
            IPackageCommitProcessor<T> processor,
            IReadOnlyList<PackageCommit> commits,
            CancellationToken token)
        {
            var stopwatch = Stopwatch.StartNew();

            var batch = new List<T>();
            foreach (var commit in commits)
            {
                foreach (var package in commit.Packages)
                {
                    try
                    {
                        var item = await processor.InitializeItemAsync(package, token);
                        if (item == null)
                        {
                            continue;
                        }

                        batch.Add(item);
                    }
                    catch
                    {
                        throw;
                    }
                }
            }

            if (batch.Any())
            {
                _log.LogInformation($"Initialized batch of {batch.Count} packages. {stopwatch.ElapsedMilliseconds}ms");
                stopwatch.Restart();
                await processor.ProcessBatchAsync(batch);
                _log.LogInformation($"Done processing batch of {batch.Count} packages. {stopwatch.ElapsedMilliseconds}ms");
            }
        }
    }
}

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
    public class CommitCollector<TEntity, TItem>
    {
        private readonly CursorService _cursorService;
        private readonly ICommitEnumerator<TEntity> _enumerator;
        private readonly ICommitProcessor<TEntity, TItem> _processor;
        private readonly ILogger _log;

        public CommitCollector(
            CursorService cursorService,
            ICommitEnumerator<TEntity> enumerator,
            ICommitProcessor<TEntity, TItem> processor,
            ILogger log)
        {
            _cursorService = cursorService;
            _enumerator = enumerator;
            _processor = processor;
            _log = log;
        }
        
        public async Task ProcessAsync(ProcessMode processMode, CancellationToken token)
        {
            var start = await _cursorService.GetValueAsync(_processor.CursorName);
            var end = await _cursorService.GetMinimumAsync(_processor.DependencyCursorNames);

            int commitCount;
            do
            {
                var commits = await _enumerator.GetCommitsAsync(
                    start,
                    end,
                    _processor.BatchSize);

                var entityCount = commits.Sum(x => x.Entities.Count);
                commitCount = commits.Count;

                if (commits.Any())
                {
                    var min = commits.Min(x => x.CommitTimestamp);
                    var max = commits.Max(x => x.CommitTimestamp);
                    start = max;
                    _log.LogInformation($"Fetched {commits.Count} commits ({entityCount} {typeof(TEntity).Name}) between {min:O} and {max:O}.");
                }
                else
                {
                    _log.LogInformation("No more commits were found within the bounds.");
                }

                switch (processMode)
                {
                    case ProcessMode.Sequentially:
                        await ProcessSequentiallyAsync(commits, token);
                        break;
                    case ProcessMode.TaskQueue:
                        await ProcessTaskQueueAsync(commits, token);
                        break;
                    default:
                        throw new NotImplementedException();
                }

                if (commits.Any())
                {
                    _log.LogInformation($"Cursor {_processor.CursorName} moving to {start:O}.");
                    await _cursorService.SetValueAsync(_processor.CursorName, start);
                }
            }
            while (commitCount > 0);
        }

        private async Task ProcessTaskQueueAsync(
            IReadOnlyList<EntityCommit<TEntity>> commits,
            CancellationToken token)
        {
            var taskQueue = new TaskQueue<IReadOnlyList<TItem>>(
                workerCount: 32,
                workAsync: x => _processor.ProcessBatchAsync(x));

            taskQueue.Start();

            foreach (var commit in commits)
            {
                var items = await _processor.InitializeItemsAsync(commit.Entities, token);
                foreach (var item in items)
                {
                    taskQueue.Enqueue(new List<TItem> { item });
                }
            }

            await taskQueue.CompleteAsync();
        }

        private async Task ProcessSequentiallyAsync(
            IReadOnlyList<EntityCommit<TEntity>> commits,
            CancellationToken token)
        {
            var stopwatch = Stopwatch.StartNew();

            var entities = commits
                .SelectMany(x => x.Entities)
                .ToList();

            var batch = await _processor.InitializeItemsAsync(entities, token);

            if (batch.Any())
            {
                _log.LogInformation($"Initialized batch of {batch.Count} {typeof(TItem).Name}. {stopwatch.ElapsedMilliseconds}ms");
                stopwatch.Restart();
                await _processor.ProcessBatchAsync(batch);
                _log.LogInformation($"Done processing batch of {batch.Count} {typeof(TItem).Name}. {stopwatch.ElapsedMilliseconds}ms");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.Delta.Common;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic
{
    public abstract class CommitCollector<TEntity, TItem>
    {
        private readonly CursorService _cursorService;
        private readonly ICommitEnumerator<TEntity> _enumerator;
        private readonly ICommitProcessor<TEntity, TItem> _processor;
        private readonly CommitCollectorSequentialProgressService _sequentialProgressService;
        private readonly ISingletonService _singletonService;
        private readonly ILogger _logger;

        public CommitCollector(
            CursorService cursorService,
            ICommitEnumerator<TEntity> enumerator,
            ICommitProcessor<TEntity, TItem> processor,
            CommitCollectorSequentialProgressService sequentialProgressService,
            ISingletonService singletonService,
            ILogger logger)
        {
            _cursorService = cursorService;
            _enumerator = enumerator;
            _processor = processor;
            _sequentialProgressService = sequentialProgressService;
            _singletonService = singletonService;
            _logger = logger;
        }
        
        public async Task ProcessAsync(ProcessMode processMode, CancellationToken token)
        {
            var start = await _cursorService.GetValueAsync(_processor.CursorName);
            var end = await _cursorService.GetMinimumAsync(_processor.DependencyCursorNames);

            int commitCount;
            do
            {
                await _singletonService.RenewAsync();

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
                    _logger.LogInformation(
                        "Fetched {CommitCount} commits ({EntityCount} {EntityName}) between {Min:O} and {Max:O}.",
                        commitCount,
                        entityCount,
                        typeof(TEntity).Name,
                        min,
                        max);

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
                        _logger.LogInformation("Cursor {CursorName} moving to {Start:O}.", _processor.CursorName, start);
                        await _cursorService.SetValueAsync(_processor.CursorName, start);
                    }
                }
                else
                {
                    _logger.LogInformation("No more commits were found within the bounds.");
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
                produceAsync: (ctx, t) => ProduceAsync(ctx, commits, t),
                consumeAsync: (x, t) => _processor.ProcessBatchAsync(x),
                logger: _logger);

            await taskQueue.RunAsync();
        }

        private async Task ProduceAsync(
            IProducerContext<IReadOnlyList<TItem>> producer,
            IReadOnlyList<EntityCommit<TEntity>> commits,
            CancellationToken token)
        {
            foreach (var commit in commits)
            {
                var skip = 0;
                var hasMore = true;
                while (hasMore)
                {
                    token.ThrowIfCancellationRequested();

                    var batch = await _processor.InitializeItemsAsync(commit.Entities, skip, token);
                    skip += batch.Items.Count;
                    hasMore = batch.HasMoreItems;

                    foreach (var item in batch.Items)
                    {
                        await producer.EnqueueAsync(new List<TItem> { item }, token);
                    }
                }
            }
        }

        private async Task ProcessSequentiallyAsync(
            IReadOnlyList<EntityCommit<TEntity>> commits,
            CancellationToken token)
        {
            var stopwatch = Stopwatch.StartNew();

            var entities = commits
                .SelectMany(x => x.Entities)
                .ToList();

            var firstCommitTimetamp = commits.Min(x => x.CommitTimestamp);
            var lastCommitTimestamp = commits.Max(x => x.CommitTimestamp);

            var skip = await _sequentialProgressService.GetSkipAsync(
                _processor.CursorName,
                firstCommitTimetamp,
                lastCommitTimestamp);
            _logger.LogInformation("Starting with skip value {Skip}.", skip);

            var hasMore = true;
            while (hasMore)
            {
                var batch = await _processor.InitializeItemsAsync(entities, skip, token);
                skip += batch.Items.Count;
                hasMore = batch.HasMoreItems;

                if (batch.Items.Any())
                {
                    _logger.LogInformation(
                        "Initialized batch of {BatchItemCount} {ItemTypeName}. {ElapsedMilliseconds}ms",
                        batch.Items.Count,
                        typeof(TItem).Name,
                        stopwatch.ElapsedMilliseconds);
                    stopwatch.Restart();
                    await _processor.ProcessBatchAsync(batch.Items);
                    await _sequentialProgressService.SetSkipAsync(
                        _processor.CursorName,
                        firstCommitTimetamp,
                        lastCommitTimestamp,
                        skip);
                    _logger.LogInformation("Setting skip value to {Skip}.", skip);
                    _logger.LogInformation(
                        "Done processing batch of {BatchItemCount} {ItemTypeName}. {ElapsedMilliseconds}ms",
                        batch.Items.Count,
                        typeof(TItem).Name,
                        stopwatch.ElapsedMilliseconds);
                }
            }
        }
    }
}

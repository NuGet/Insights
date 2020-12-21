using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.Delta.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Entities
{
    public abstract class CommitCollector<TEntity, TItem, TProgressToken>
    {
        private readonly CursorService _cursorService;
        private readonly ICommitEnumerator<TEntity> _enumerator;
        private readonly ICommitProcessor<TEntity, TItem, TProgressToken> _processor;
        private readonly CommitCollectorSequentialProgressService _sequentialProgressService;
        private readonly ISingletonService _singletonService;
        private readonly IOptions<ExplorePackagesEntitiesSettings> _options;
        private readonly ILogger _logger;

        public CommitCollector(
            CursorService cursorService,
            ICommitEnumerator<TEntity> enumerator,
            ICommitProcessor<TEntity, TItem, TProgressToken> processor,
            CommitCollectorSequentialProgressService sequentialProgressService,
            ISingletonService singletonService,
            IOptions<ExplorePackagesEntitiesSettings> options,
            ILogger logger)
        {
            _cursorService = cursorService;
            _enumerator = enumerator;
            _processor = processor;
            _sequentialProgressService = sequentialProgressService;
            _singletonService = singletonService;
            _options = options;
            _logger = logger;
        }

        public async Task ProcessAsync(CancellationToken token)
        {
            var start = await _cursorService.GetValueAsync(_processor.CursorName);

            var dependencyCursorNames = _processor
                .DependencyCursorNames
                .Concat(new[] { CursorNames.CatalogToDatabase })
                .ToList();
            var end = await _cursorService.GetMinimumAsync(dependencyCursorNames);

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

                    switch (_processor.ProcessMode)
                    {
                        case ProcessMode.Sequentially:
                            await ProcessSequentiallyAsync(commits, token);
                            break;
                        case ProcessMode.TaskQueue:
                            await ProcessTaskQueueAsync(commits);
                            break;
                        default:
                            throw new NotImplementedException();
                    }

                    if (commits.Any())
                    {
                        await _cursorService.SetValueAsync(_processor.CursorName, start);
                        _logger.LogInformation("[CHECKPOINT] Cursor {CursorName} moved to {Start:O}.", _processor.CursorName, start);
                    }
                }
                else
                {
                    _logger.LogInformation("No more commits were found within the bounds.");
                }
            }
            while (commitCount > 0);
        }

        private async Task ProcessTaskQueueAsync(IReadOnlyList<EntityCommit<TEntity>> commits)
        {
            var taskQueue = new TaskQueue<IReadOnlyList<TItem>>(
                workerCount: _options.Value.WorkerCount,
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
                var progressToken = default(TProgressToken);
                var hasMoreItems = true;
                while (hasMoreItems)
                {
                    token.ThrowIfCancellationRequested();

                    var batch = await _processor.InitializeItemsAsync(commit.Entities, progressToken, token);
                    progressToken = batch.NextProgressToken;
                    hasMoreItems = batch.HasMoreItems;

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
            var entities = commits
                .SelectMany(x => x.Entities)
                .ToList();

            var firstCommitTimetamp = commits.Min(x => x.CommitTimestamp);
            var lastCommitTimestamp = commits.Max(x => x.CommitTimestamp);

            var serializedProgressToken = await _sequentialProgressService.GetSerializedProgressTokenAsync(
                _processor.CursorName,
                firstCommitTimetamp,
                lastCommitTimestamp);
            _logger.LogInformation("Starting with progress token {ProgressToken}.", serializedProgressToken);
            var progressToken = _processor.DeserializeProgressToken(serializedProgressToken);

            var hasMoreItems = true;
            while (hasMoreItems)
            {
                var stopwatch = Stopwatch.StartNew();
                var batch = await _processor.InitializeItemsAsync(entities, progressToken, token);
                progressToken = batch.NextProgressToken;
                hasMoreItems = batch.HasMoreItems;

                if (batch.Items.Any())
                {
                    _logger.LogInformation(
                        "Initialized batch of {BatchItemCount} {ItemTypeName}. {ElapsedMilliseconds}ms",
                        batch.Items.Count,
                        typeof(TItem).Name,
                        stopwatch.ElapsedMilliseconds);
                    stopwatch.Restart();
                    await _processor.ProcessBatchAsync(batch.Items);

                    if (hasMoreItems)
                    {
                        var serializedNextProgressToken = _processor.SerializeProgressToken(progressToken);
                        await _sequentialProgressService.SetSerializedProgressTokenAsync(
                            _processor.CursorName,
                            firstCommitTimetamp,
                            lastCommitTimestamp,
                            serializedNextProgressToken);
                        _logger.LogInformation("[CHECKPOINT] Set next progress token to {ProgressToken}.", serializedNextProgressToken);
                    }

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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.CatalogReader;

namespace Knapcode.ExplorePackages.Logic
{
    public class CatalogProcessorQueue
    {
        private readonly TaskQueue<Work> _taskQueue;
        private readonly CatalogReader _catalogReader;
        private readonly ICatalogEntriesProcessor _processor;

        public CatalogProcessorQueue(
            CatalogReader catalogReader,
            ICatalogEntriesProcessor processor)
        {
            _catalogReader = catalogReader;
            _processor = processor;
            _taskQueue = new TaskQueue<Work>(
                workerCount: 1,
                workAsync: WorkAsync);
        }

        public async Task ProcessAsync(CancellationToken token)
        {
            var cursorService = new CursorService();
            var start = await cursorService.GetValueAsync(_processor.CursorName);
            DateTimeOffset end;
            var dependencyCursorNames = _processor.DependencyCursorNames;
            if (dependencyCursorNames.Any())
            {
                end = await cursorService.GetMinimumAsync(dependencyCursorNames);
            }
            else
            {
                end = DateTimeOffset.UtcNow;
            }

            _taskQueue.Start();
            var failureTask = _taskQueue.FailureTask;
            var produceThenCompleteTask = ProduceThenCompleteAsync(start, end, token);
            var firstTask = await Task.WhenAny(failureTask, produceThenCompleteTask);
            if (firstTask == failureTask)
            {
                await await failureTask;
            }
            else
            {
                await produceThenCompleteTask;
            }
        }

        private async Task ProduceThenCompleteAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken token)
        {
            await ProduceAsync(start, end, token);
            await _taskQueue.CompleteAsync();
        }

        private async Task WorkAsync(Work work)
        {
            if (!work.Leaves.Any())
            {
                return;
            }

            await _processor.ProcessAsync(work.Page, work.Leaves);

            var cursorService = new CursorService();
            await cursorService.SetValueAsync(_processor.CursorName, work.Leaves.Last().CommitTimeStamp);
        }

        private async Task ProduceAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken token)
        {
            var remainingPages = new Queue<CatalogPageEntry>(await _catalogReader.GetPageEntriesAsync(start, end, token));

            while (remainingPages.Any())
            {
                var currentPage = remainingPages.Dequeue();
                var currentPages = new[] { currentPage };

                var entries = await _catalogReader.GetEntriesAsync(currentPages, start, end, token);

                // Each processor should ensure values are sorted in an appropriate fashion, but for consistency we
                // sort here as well.
                entries = entries
                    .OrderBy(x => x.CommitTimeStamp)
                    .ThenBy(x => x.Id)
                    .ThenBy(x => x.Version)
                    .ToList();

                _taskQueue.Enqueue(new Work(currentPage, entries));

                while (_taskQueue.Count > 10)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }
        }

        private class Work
        {
            public Work(CatalogPageEntry page, IReadOnlyList<CatalogEntry> leaves)
            {
                Page = page;
                Leaves = leaves;
            }

            public CatalogPageEntry Page { get; }
            public IReadOnlyList<CatalogEntry> Leaves { get; }
        }
    }
}

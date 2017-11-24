using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Support;
using NuGet.CatalogReader;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Logic
{
    public class CatalogProcessorQueue
    {
        private readonly TaskQueue<IReadOnlyList<CatalogEntry>> _taskQueue;
        private readonly CatalogReader _catalogReader;
        private readonly ICatalogEntriesProcessor _processor;
        private readonly ILogger _log;

        public CatalogProcessorQueue(
            CatalogReader catalogReader,
            ICatalogEntriesProcessor processor,
            ILogger log)
        {
            _catalogReader = catalogReader;
            _processor = processor;
            _log = log;
            _taskQueue = new TaskQueue<IReadOnlyList<CatalogEntry>>(
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
            await ProduceAsync(start, end, token);
            await _taskQueue.CompleteAsync();
        }

        private async Task WorkAsync(IReadOnlyList<CatalogEntry> entries)
        {
            try
            {
                if (!entries.Any())
                {
                    return;
                }

                await _processor.ProcessAsync(entries);

                var cursorService = new CursorService();
                await cursorService.SetValueAsync(_processor.CursorName, entries.Last().CommitTimeStamp);
            }
            catch (Exception e)
            {
                _log.LogError(e.ToString());
                throw;
            }
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

                _taskQueue.Enqueue(entries);

                while (_taskQueue.Count > 10)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }
        }
    }
}

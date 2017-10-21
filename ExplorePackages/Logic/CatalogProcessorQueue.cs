using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Knapcode.ExplorePackages.Support;
using NuGet.CatalogReader;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Logic
{
    public class CatalogProcessorQueue
    {
        private readonly TaskQueue<IReadOnlyList<CatalogEntry>> _taskQueue;
        private readonly ICatalogEntriesProcessor _processor;
        private readonly ILogger _log;

        public CatalogProcessorQueue(
            ICatalogEntriesProcessor processor,
            ILogger log)
        {
            _processor = processor;
            _log = log;
            _taskQueue = new TaskQueue<IReadOnlyList<CatalogEntry>>(
                workerCount: 1,
                workAsync: WorkAsync);
        }

        public async Task ProcessAsync(CancellationToken token)
        {
            DateTimeOffset start;
            DateTimeOffset end;
            using (var entityContext = new EntityContext())
            {
                var cursorService = new CursorService(entityContext);
                start = await cursorService.GetAsync(_processor.CursorName);

                var dependencyCursorNames = _processor.DependencyCursorNames;
                if (dependencyCursorNames.Any())
                {
                    end = await cursorService.GetMinimumAsync(dependencyCursorNames);
                }
                else
                {
                    end = DateTimeOffset.UtcNow;
                }
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

                using (var entityContext = new EntityContext())
                {
                    var cursorService = new CursorService(entityContext);
                    await cursorService.SetAsync(_processor.CursorName, entries.Last().CommitTimeStamp);
                }
            }
            catch (Exception e)
            {
                _log.LogError(e.ToString());
                throw;
            }
        }

        private async Task ProduceAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken token)
        {
            using (var catalogReader = new CatalogReader(new Uri("https://api.nuget.org/v3/index.json"), _log))
            {
                var remainingPages = new Queue<CatalogPageEntry>(await catalogReader.GetPageEntriesAsync(start, end, token));

                while (remainingPages.Any())
                {
                    var currentPage = remainingPages.Dequeue();
                    var currentPages = new[] { currentPage };

                    var entries = await catalogReader.GetEntriesAsync(currentPages, start, end, token);
                    
                    _taskQueue.Enqueue(entries);

                    while (_taskQueue.Count > 10)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1));
                    }
                }
            }
        }
    }
}

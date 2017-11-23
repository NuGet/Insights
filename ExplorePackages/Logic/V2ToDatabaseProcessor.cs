using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Support;

namespace Knapcode.ExplorePackages.Logic
{
    public class V2ToDatabaseProcessor
    {
        private static readonly TimeSpan FuzzFactor = TimeSpan.FromHours(1);
        private const int PageSize = 100;
        private readonly CursorService _cursorService;
        private readonly V2Client _v2Client;
        private readonly V2PackageEntityService _service;
        private readonly ExplorePackagesSettings _settings;

        public V2ToDatabaseProcessor(
            CursorService cursorService,
            V2Client v2Client,
            V2PackageEntityService service,
            ExplorePackagesSettings settings)
        {
            _cursorService = cursorService;
            _v2Client = v2Client;
            _service = service;
            _settings = settings;
        }

        public async Task UpdateAsync()
        {
            var taskQueue = new TaskQueue<IReadOnlyList<V2Package>>(
                workerCount: 1,
                workAsync: ConsumeAsync);

            taskQueue.Start();

            await ProduceAsync(taskQueue);

            await taskQueue.CompleteAsync();
        }

        private async Task ProduceAsync(TaskQueue<IReadOnlyList<V2Package>> taskQueue)
        {
            var start = await _cursorService.GetAsync(CursorNames.V2ToDatabase);
            if (start > DateTimeOffset.MinValue.Add(FuzzFactor))
            {
                start = start.Subtract(FuzzFactor);
            }

            var complete = false;
            do
            {
                var packages = await _v2Client.GetPackagesAsync(
                   _settings.V2BaseUrl,
                   V2OrderByTimestamp.Created,
                   start,
                   PageSize);

                // If we have a full page, take only packages with a created timestamp less than the max.
                if (packages.Count == PageSize)
                {
                    var max = packages.Max(x => x.Created);
                    var packagesBeforeMax = packages
                        .Where(x => x.Created < max)
                        .ToList();

                    if (packages.Any()
                        && !packagesBeforeMax.Any())
                    {
                        throw new InvalidOperationException("All of the packages in the page have the same created timestamp.");
                    }

                    packages = packagesBeforeMax;
                }
                else
                {
                    complete = true;
                }

                while (taskQueue.Count > 50)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
                
                if (packages.Count > 0)
                {
                    taskQueue.Enqueue(packages);
                    start = packages.Max(x => x.Created);
                }
            }
            while (!complete);
        }

        private async Task ConsumeAsync(IReadOnlyList<V2Package> packages)
        {
            var oldCUrsor = await _cursorService.GetAsync(CursorNames.V2ToDatabase);

            await _service.AddOrUpdatePackagesAsync(packages);

            var newCursor = packages.Max(x => x.Created);
            if (newCursor > oldCUrsor)
            {
                await _cursorService.SetAsync(CursorNames.V2ToDatabase, newCursor);
            }
        }
    }
}

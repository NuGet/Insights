using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Support;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageDownloadsToDatabaseProcessor
    {
        private readonly PackageDownloadsClient _client;
        private readonly PackageService _service;
        private readonly ETagService _etagService;

        public PackageDownloadsToDatabaseProcessor(
            PackageDownloadsClient client,
            PackageService service,
            ETagService etagService)
        {
            _client = client;
            _service = service;
            _etagService = etagService;
        }

        public async Task UpdateAsync()
        {
            var previousETag = await _etagService.GetValueAsync(ETagNames.DownloadsV1);

            var taskQueue = new TaskQueue<IReadOnlyList<PackageDownloads>>(
                workerCount: 1,
                workAsync: ConsumeAsync);

            taskQueue.Start();

            var newETag = await ProduceAsync(previousETag, taskQueue);

            await taskQueue.CompleteAsync();

            await _etagService.SetValueAsync(ETagNames.DownloadsV1, newETag);
        }

        private async Task<string> ProduceAsync(string previousETag, TaskQueue<IReadOnlyList<PackageDownloads>> taskQueue)
        {
            var batch = new List<PackageDownloads>();
            using (var packageDownloadSet = await _client.GetPackageDownloadSetAsync(previousETag))
            using (var enumerator = packageDownloadSet.Downloads)
            {
                while (await enumerator.MoveNext())
                {
                    batch.Add(enumerator.Current);
                    if (batch.Count >= 1000)
                    {
                        taskQueue.Enqueue(batch);
                        batch = new List<PackageDownloads>();
                    }
                }

                if (batch.Any())
                {
                    taskQueue.Enqueue(batch);
                }

                return packageDownloadSet.ETag;
            }
        }

        private async Task ConsumeAsync(IReadOnlyList<PackageDownloads> packages)
        {
            await _service.AddOrUpdatePackagesAsync(packages);
        }
    }
}

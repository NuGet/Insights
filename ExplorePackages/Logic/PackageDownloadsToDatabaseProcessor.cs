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

        public PackageDownloadsToDatabaseProcessor(
            PackageDownloadsClient client,
            PackageService service)
        {
            _client = client;
            _service = service;
        }

        public async Task UpdateAsync()
        {
            var taskQueue = new TaskQueue<IReadOnlyList<PackageDownloads>>(
                workerCount: 1,
                workAsync: ConsumeAsync);

            taskQueue.Start();

            await ProduceAsync(taskQueue);

            await taskQueue.CompleteAsync();
        }

        private async Task ProduceAsync(TaskQueue<IReadOnlyList<PackageDownloads>> taskQueue)
        {
            var batch = new List<PackageDownloads>();
            var packageDownloads = _client.GetPackageDownloads();
            using (var enumerator = packageDownloads.GetEnumerator())
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
            }
        }

        private async Task ConsumeAsync(IReadOnlyList<PackageDownloads> packages)
        {
            await _service.AddOrUpdatePackagesAsync(packages);
        }
    }
}

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Knapcode.ExplorePackages.Support;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Logic
{
    public class DatabaseToMZipProcessor
    {
        private readonly PackageService _packageService;
        private readonly CursorService _cursorService;
        private readonly MZipDownloader _mZipDownloader;
        private readonly ILogger _log;

        public DatabaseToMZipProcessor(
            PackageService packageService,
            CursorService cursorService,
            MZipDownloader mZipDownloader,
            ILogger log)
        {
            _packageService = packageService;
            _cursorService = cursorService;
            _mZipDownloader = mZipDownloader;
            _log = log;
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            var start = await _cursorService.GetValueAsync(CursorNames.DatabaseToMZip);
            var end = await _cursorService.GetMinimumAsync(new[]
            {
                CursorNames.NuGetOrg.FlatContainer,
                CursorNames.CatalogToDatabase,
            });

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

                var taskQueue = new TaskQueue<PackageEntity>(
                    workerCount: 32,
                    workAsync: x => StoreMZipAsync(x, token));

                taskQueue.Start();

                foreach (var commit in commits)
                {
                    foreach (var package in commit.Packages)
                    {
                        if (package.CatalogPackage.Deleted)
                        {
                            continue;
                        }

                        taskQueue.Enqueue(package);
                    }
                }

                await taskQueue.CompleteAsync();

                if (commits.Any())
                {
                    _log.LogInformation($"Cursor {CursorNames.DatabaseToMZip} moving to {start:O}.");
                    await _cursorService.SetValueAsync(CursorNames.DatabaseToMZip, start);
                }
            }
            while (commitCount > 0);
        }

        private async Task StoreMZipAsync(PackageEntity package, CancellationToken token)
        {
            await _mZipDownloader.StoreMZipAsync(
                package.PackageRegistration.Id,
                package.Version,
                token);
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Knapcode.ExplorePackages.Logic;
using Knapcode.ExplorePackages.Support;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Commands
{
    public class DatabaseToMZipCommand : ICommand
    {
        private readonly PackageService _packageService;
        private readonly CursorService _cursorService;
        private readonly MZipDownloader _mZipDownloader;
        private readonly ILogger _log;

        public DatabaseToMZipCommand(
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

        public async Task ExecuteAsync(IReadOnlyList<string> args, CancellationToken token)
        {
            var start = await _cursorService.GetValueAsync(CursorNames.DatabaseToMZip);
            var end = await _cursorService.GetMinimumAsync(new[] { CursorNames.NuGetOrg.FlatContainer });

            int commitCount;
            do
            {
                var commits = await _packageService.GetPackageCommitsAsync(start, end);
                commitCount = commits.Count;

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

                if (commitCount > 0)
                {
                    start = commits.Max(x => x.CommitTimestamp);
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

        public bool IsDatabaseRequired(IReadOnlyList<string> args)
        {
            return true;
        }
    }
}

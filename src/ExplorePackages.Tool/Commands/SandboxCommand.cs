using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using McMaster.Extensions.CommandLineUtils;

namespace Knapcode.ExplorePackages.Tool.Commands
{
    public class SandboxCommand : ICommand
    {
        private readonly IFileStorageService _fileStorageService;
        private readonly CursorService _cursorService;
        private readonly PackageCommitEnumerator _packageCommitEnumerator;
        private readonly ExplorePackagesSettings _settings;

        public SandboxCommand(
            IFileStorageService fileStorageService,
            CursorService cursorService,
            PackageCommitEnumerator packageCommitEnumerator,
            ExplorePackagesSettings settings)
        {
            _fileStorageService = fileStorageService;
            _cursorService = cursorService;
            _packageCommitEnumerator = packageCommitEnumerator;
            _settings = settings;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            const string cursorName = "TEMP_CopyFileToBlob";

            var start = await _cursorService.GetValueAsync(cursorName);
            var end = DateTimeOffset.MaxValue;

            var commitCount = 1;
            do
            {
                Console.Write($"Start: {start:O}...");
                var commits = await _packageCommitEnumerator.GetCommitsAsync(
                    start,
                    end,
                    batchSize: 5000);
                commitCount = commits.Count;
                Console.WriteLine($" {commitCount} commits.");

                await TaskProcessor.ExecuteAsync(
                    commits.SelectMany(x => x.Entities),
                    async package =>
                    {
                        Console.WriteLine($"{package.Id} {package.Version}");

                        await _fileStorageService.CopyMZipFileToBlobIfExistsAsync(package.Id, package.Version);
                        await _fileStorageService.CopyNuspecFileToBlobIfExistsAsync(package.Id, package.Version);

                        return true;
                    },
                    workerCount: 32);

                if (commits.Any())
                {
                    start = commits.Max(x => x.CommitTimestamp);
                    await _cursorService.SetValueAsync(cursorName, start);
                }
            }
            while (commitCount > 0);
        }

        public bool IsDatabaseRequired()
        {
            return true;
        }
    }
}

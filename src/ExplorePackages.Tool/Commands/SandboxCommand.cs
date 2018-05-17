using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Tool.Commands
{
    public class SandboxCommand : ICommand
    {
        private readonly ExplorePackagesSettings _settings;
        private readonly ILogger<FileStorageService> _logger;

        public SandboxCommand(ExplorePackagesSettings settings, ILogger<FileStorageService> logger)
        {
            _settings = settings;
            _logger = logger;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            var oldProvider = new PackageFilePathProvider(_settings, style: PackageFilePathStyle.FourIdLetters);
            var newProvider = new PackageFilePathProvider(_settings, style: PackageFilePathStyle.TwoByteIdentityHash);

            var packageCommitEnumerator = new PackageCommitEnumerator();

            var start = DateTimeOffset.Parse("2018-05-16T18:20:19.9174542+00:00");
            var end = DateTimeOffset.MaxValue;

            var commitCount = 1;
            do
            {
                Console.Write($"Start: {start:O}...");
                var commits = await packageCommitEnumerator.GetCommitsAsync(
                    start,
                    end,
                    batchSize: 5000);
                commitCount = commits.Count;
                Console.WriteLine($" {commitCount} commits.");

                await TaskProcessor.ExecuteAsync(
                    commits.SelectMany(x => x.Entities),
                    package =>
                    {
                        Console.WriteLine($" - {package.Id} {package.Version}");

                        var packageSpecificDir = oldProvider.GetPackageSpecificDirectory(package.Id, package.Version);
                        if (!Directory.Exists(packageSpecificDir))
                        {
                            return Task.FromResult(true);
                        }

                        try
                        {
                            var oldPath = oldProvider.GetLatestMZipFilePath(package.Id, package.Version);
                            var newPath = newProvider.GetLatestMZipFilePath(package.Id, package.Version);
                            Directory.CreateDirectory(Path.GetDirectoryName(newPath));
                            File.Move(oldPath, newPath);
                        }
                        catch (IOException)
                        {
                        }

                        try
                        {
                            var oldPath = oldProvider.GetLatestNuspecFilePath(package.Id, package.Version);
                            var newPath = newProvider.GetLatestNuspecFilePath(package.Id, package.Version);
                            Directory.CreateDirectory(Path.GetDirectoryName(newPath));
                            File.Move(oldPath, newPath);
                        }
                        catch (IOException)
                        {
                        }

                        return Task.FromResult(true);
                    },
                    workerCount: 4);

                if (commits.Any())
                {
                    start = commits.Max(x => x.CommitTimestamp);
                }
            }
            while (commitCount > 0);

            Console.WriteLine("Deleting empty directories...");
            foreach (var dir in Directory.EnumerateDirectories(_settings.PackagePath).Reverse())
            {
                if (Path.GetFileName(dir).Length == 1)
                {
                    FileUtility.DeleteEmptyDirectories(dir);
                    FileUtility.DeleteDirectoryIfEmpty(dir);
                }
            }
        }

        public bool IsDatabaseRequired()
        {
            return true;
        }
    }
}

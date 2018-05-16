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
            var oldProvider = new PackageFilePathProvider(_settings, style: PackageFilePathStyle.IdVersion);
            var newProvider = new PackageFilePathProvider(_settings, style: PackageFilePathStyle.FourIdLetters);

            var blobNameProvider = new PackageBlobNameProvider();

            var oldService = new FileStorageService(
                oldProvider,
                blobNameProvider,
                _settings,
                _logger);
            var newService = new FileStorageService(
                newProvider,
                blobNameProvider,
                _settings,
                _logger);

            var packageCommitEnumerator = new PackageCommitEnumerator();

            var start = DateTimeOffset.Parse("2018-01-15T03:25:03.2918890+00:00");
            var end = DateTimeOffset.MaxValue;

            var commitCount = 1;
            do
            {
                Console.Write($"Start: {start:O}...");
                var commits = await packageCommitEnumerator.GetCommitsAsync(
                    start,
                    end,
                    batchSize: 2000);
                commitCount = commits.Count;
                Console.WriteLine($" {commitCount} commits.");

                foreach (var commit in commits)
                {
                    foreach (var package in commit.Entities)
                    {
                        Console.WriteLine($" - {package.Id} {package.Version}");

                        var packageSpecificDir = oldProvider.GetPackageSpecificDirectory(package.Id, package.Version);
                        var idSpecificDir = Path.GetDirectoryName(packageSpecificDir);

                        if (!Directory.Exists(idSpecificDir))
                        {
                            continue;
                        }

                        if (!Directory.Exists(packageSpecificDir))
                        {
                            FileUtility.DeleteDirectoryIfEmpty(idSpecificDir);
                            continue;
                        }

                        using (var srcStream = await oldService.GetMZipStreamOrNullAsync(package.Id, package.Version))
                        {
                            if (srcStream != null)
                            {
                                await newService.StoreMZipStreamAsync(
                                    package.Id,
                                    package.Version,
                                    destStream => srcStream.CopyToAsync(destStream));
                            }
                        }

                        await oldService.DeleteMZipStreamAsync(package.Id, package.Version);

                        using (var srcStream = await oldService.GetNuspecStreamOrNullAsync(package.Id, package.Version))
                        {
                            if (srcStream != null)
                            {
                                await newService.StoreNuspecStreamAsync(
                                    package.Id,
                                    package.Version,
                                    destStream => srcStream.CopyToAsync(destStream));
                            }
                        }

                        await oldService.DeleteNuspecStreamAsync(package.Id, package.Version);

                        FileUtility.DeleteEmptyDirectories(packageSpecificDir);
                        FileUtility.DeleteDirectoryIfEmpty(packageSpecificDir);
                        FileUtility.DeleteDirectoryIfEmpty(idSpecificDir);
                    }
                }

                start = commits.Max(x => x.CommitTimestamp);
            }
            while (commitCount > 0);
        }

        public bool IsDatabaseRequired()
        {
            return true;
        }
    }
}

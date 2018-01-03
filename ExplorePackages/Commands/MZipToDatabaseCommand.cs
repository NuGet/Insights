using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Knapcode.ExplorePackages.Logic;
using Knapcode.MiniZip;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Commands
{
    public class MZipToDatabaseCommand : ICommand
    {
        private readonly CursorService _cursorService;
        private readonly PackageService _packageService;
        private readonly MZipStore _mZipStore;
        private readonly ILogger _log;

        public MZipToDatabaseCommand(
            CursorService cursorService,
            PackageService packageService,
            MZipStore mZipStore,
            ILogger log)
        {
            _cursorService = cursorService;
            _packageService = packageService;
            _mZipStore = mZipStore;
            _log = log;
        }

        public async Task ExecuteAsync(IReadOnlyList<string> args, CancellationToken token)
        {
            var start = await _cursorService.GetValueAsync(CursorNames.MZipToDatabase);
            var end = await _cursorService.GetMinimumAsync(new[]
            {
                CursorNames.CatalogToDatabase,
                CursorNames.MZip,
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

                var stopwatch = Stopwatch.StartNew();
                var batch = new List<PackageArchiveMetadata>();
                foreach (var commit in commits)
                {
                    foreach (var package in commit.Packages)
                    {
                        var metadata = await GetPackageArchiveMetadataAsync(package, token);
                        if (metadata == null)
                        {
                            continue;
                        }

                        batch.Add(metadata);
                    }
                }

                // Save the metadata.
                if (batch.Any())
                {
                    _log.LogInformation($"Got metadata about {batch.Count} package archives. {stopwatch.ElapsedMilliseconds}ms");
                    await _packageService.AddOrUpdatePackagesAsync(batch);
                }

                if (commits.Any())
                {
                    _log.LogInformation($"Cursor {CursorNames.MZipToDatabase} moving to {start:O}.");
                    await _cursorService.SetValueAsync(CursorNames.MZipToDatabase, start);
                }
            }
            while (commitCount > 0);
        }

        private async Task<PackageArchiveMetadata> GetPackageArchiveMetadataAsync(
            PackageEntity package,
            CancellationToken token)
        {
            // Read the .zip directory.
            ZipDirectory zipDirectory;
            long size;
            using (var stream = await _mZipStore.GetMZipStreamAsync(
                package.PackageRegistration.Id,
                package.Version,
                token))
            {
                if (stream == null)
                {
                    if (!package.CatalogPackage.Deleted)
                    {
                        throw new InvalidOperationException($"Could not find .mzip for {package.PackageRegistration.Id} {package.Version}.");
                    }

                    return null;
                }

                using (var reader = new ZipDirectoryReader(stream))
                {
                    zipDirectory = await reader.ReadAsync();
                    size = stream.Length;
                }
            }

            // Gather the metadata.
            var metadata = new PackageArchiveMetadata
            {
                Id = package.PackageRegistration.Id,
                Version = package.Version,
                Size = size,
                ZipDirectory = zipDirectory,
            };

            return metadata;
        }

        public bool IsDatabaseRequired(IReadOnlyList<string> args)
        {
            return true;
        }
    }
}

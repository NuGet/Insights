using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Knapcode.ExplorePackages.Logic;
using McMaster.Extensions.CommandLineUtils;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Commands
{
    public class DependenciesToDatabaseCommand : ICommand
    {
        private readonly CursorService _cursorService;
        private readonly PackageService _packageService;
        private readonly PackageDependencyService _packageDependencyService;
        private readonly NuspecProvider _nuspecProvider;
        private readonly ILogger _log;

        public DependenciesToDatabaseCommand(
            CursorService cursorService,
            PackageService packageService,
            PackageDependencyService packageDependencyService,
            NuspecProvider nuspecProvider,
            ILogger log)
        {
            _cursorService = cursorService;
            _packageService = packageService;
            _packageDependencyService = packageDependencyService;
            _nuspecProvider = nuspecProvider;
            _log = log;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            var start = await _cursorService.GetValueAsync(CursorNames.DependenciesToDatabase);
            var end = await _cursorService.GetMinimumAsync(new[]
            {
                CursorNames.CatalogToNuspecs,
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

                var stopwatch = Stopwatch.StartNew();
                var batch = new List<PackageDependencyGroups>();
                foreach (var commit in commits)
                {
                    foreach (var package in commit.Packages)
                    {
                        var packageDependencyGroups = GetPackageDependencyGroups(package, token);
                        if (packageDependencyGroups == null)
                        {
                            continue;
                        }

                        batch.Add(packageDependencyGroups);
                    }
                }

                // Save the metadata.
                if (batch.Any())
                {
                    _log.LogInformation($"Got dependencies for {batch.Count} packages. {stopwatch.ElapsedMilliseconds}ms");
                    await _packageDependencyService.AddDependenciesAsync(batch);
                }

                if (commits.Any())
                {
                    _log.LogInformation($"Cursor {CursorNames.DependenciesToDatabase} moving to {start:O}.");
                    await _cursorService.SetValueAsync(CursorNames.DependenciesToDatabase, start);
                }
            }
            while (commitCount > 0);
        }

        private PackageDependencyGroups GetPackageDependencyGroups(PackageEntity package, CancellationToken token)
        {
            var nuspec = _nuspecProvider.GetNuspec(package.PackageRegistration.Id, package.Version);
            if (nuspec.Document == null)
            {
                return null;
            }

            var identity = new PackageIdentity(package.PackageRegistration.Id, package.Version);
            var dependencyGroups = NuspecUtility.GetParsedDependencyGroups(nuspec.Document);

            return new PackageDependencyGroups(identity, dependencyGroups);
        }

        public bool IsDatabaseRequired()
        {
            return true;
        }
    }
}

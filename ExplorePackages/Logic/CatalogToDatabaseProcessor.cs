using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using NuGet.CatalogReader;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Logic
{
    public class CatalogToDatabaseProcessor : ICatalogEntriesProcessor
    {
        private readonly ILogger _log;

        public CatalogToDatabaseProcessor(ILogger log)
        {
            _log = log;
        }

        public IReadOnlyList<string> DependencyCursorNames => new List<string>();

        public string CursorName => CursorNames.CatalogToDatabase;

        public async Task ProcessAsync(IReadOnlyList<CatalogEntry> entries)
        {
            var cursorService = new CursorService();
            var packageService = new PackageService(_log);

            // Add the new entries.
            await packageService.AddOrUpdateBatchAsync(entries);

            // Delete query results for modified (deleted, edited, reflowed, etc.) packages.
            var start = await cursorService.GetAsync(CursorName);
            var end = entries.Last().CommitTimeStamp;

            var commits = await packageService.GetAllPackageCommitsAsync(start, end);
            var packageKeys = commits
                .SelectMany(x => x.Packages)
                .Select(x => x.Key)
                .ToList();

            var packageQueryService = new PackageQueryService(_log);
            await packageQueryService.DeleteResultsForPackagesAsync(packageKeys);
        }
    }
}

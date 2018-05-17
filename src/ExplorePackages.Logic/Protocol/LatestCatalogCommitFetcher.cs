using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.CatalogReader;

namespace Knapcode.ExplorePackages.Logic
{
    public class LatestCatalogCommitFetcher
    {
        private readonly CatalogReader _catalogReader;

        public LatestCatalogCommitFetcher(CatalogReader catalogReader)
        {
            _catalogReader = catalogReader;
        }

        public async Task<IReadOnlyList<CatalogEntry>> GetLatestCommitAsync(IProgressReporter progressReporter)
        {
            var pages = await _catalogReader.GetPageEntriesAsync(CancellationToken.None);
            await progressReporter.ReportProgressAsync(0.5m, $"Found {pages.Count} catalog pages.");

            var lastPage = pages
                .OrderBy(x => x.CommitTimeStamp)
                .Last();

            var entries = await _catalogReader.GetEntriesAsync(new[] { lastPage }, CancellationToken.None);
            await progressReporter.ReportProgressAsync(1, $"Found {entries.Count} catalog items in the latest page.");

            var commit = entries
                .GroupBy(x => x.CommitTimeStamp)
                .OrderBy(x => x.Key)
                .Last()
                .ToList();
            await progressReporter.ReportProgressAsync(1, $"Found catalog commit {commit.First().CommitTimeStamp:O} containing {commit.Count} items.");

            return commit;
        }
    }
}

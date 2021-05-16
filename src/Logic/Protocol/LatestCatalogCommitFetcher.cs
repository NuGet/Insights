using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuGet.Insights
{
    public class LatestCatalogCommitFetcher
    {
        private readonly CatalogClient _catalogReader;

        public LatestCatalogCommitFetcher(CatalogClient catalogReader)
        {
            _catalogReader = catalogReader;
        }

        public async Task<IReadOnlyList<CatalogLeafItem>> GetLatestCommitAsync(IProgressReporter progressReporter)
        {
            var index = await _catalogReader.GetCatalogIndexAsync();
            await progressReporter.ReportProgressAsync(0.5m, $"Found {index.Items.Count} catalog pages.");

            var lastPageItem = index
                .Items
                .OrderBy(x => x.CommitTimestamp)
                .Last();

            var lastPage = await _catalogReader.GetCatalogPageAsync(lastPageItem.Url);
            await progressReporter.ReportProgressAsync(1, $"Found {lastPage.Items.Count} catalog items in the latest page.");

            var commit = lastPage
                .Items
                .GroupBy(x => x.CommitTimestamp)
                .OrderBy(x => x.Key)
                .Last()
                .ToList();
            await progressReporter.ReportProgressAsync(1, $"Found catalog commit {commit.First().CommitTimestamp:O} containing {commit.Count} items.");

            return commit;
        }
    }
}

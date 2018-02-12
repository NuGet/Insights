using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.CatalogReader;

namespace Knapcode.ExplorePackages.Logic
{
    public class CatalogToDatabaseProcessor : ICatalogEntriesProcessor
    {
        private readonly PackageService _packageService;
        private readonly CatalogService _catalogService;

        public CatalogToDatabaseProcessor(
            PackageService packageService,
            CatalogService catalogService)
        {
            _packageService = packageService;
            _catalogService = catalogService;
        }

        public IReadOnlyList<string> DependencyCursorNames => new List<string>();

        public string CursorName => CursorNames.CatalogToDatabase;

        public async Task ProcessAsync(CatalogPageEntry page, IReadOnlyList<CatalogEntry> leaves)
        {
            var identityToPackageKey = await _packageService.AddOrUpdatePackagesAsync(leaves);

            // await _catalogService.AddOrUpdateAsync(page, leaves, identityToPackageKey);
        }
    }
}

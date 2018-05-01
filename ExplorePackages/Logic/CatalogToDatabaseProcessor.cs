using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Support;
using NuGet.CatalogReader;

namespace Knapcode.ExplorePackages.Logic
{
    public class CatalogToDatabaseProcessor : ICatalogEntriesProcessor
    {
        private readonly IPackageService _packageService;
        private readonly CatalogService _catalogService;
        private readonly V2Client _v2Client;
        private readonly ExplorePackagesSettings _settings;

        public CatalogToDatabaseProcessor(
            IPackageService packageService,
            CatalogService catalogService,
            V2Client v2Client,
            ExplorePackagesSettings settings)
        {
            _packageService = packageService;
            _catalogService = catalogService;
            _v2Client = v2Client;
            _settings = settings;
        }

        public IReadOnlyList<string> DependencyCursorNames => new List<string>();

        public string CursorName => CursorNames.CatalogToDatabase;

        public async Task ProcessAsync(CatalogPageEntry page, IReadOnlyList<CatalogEntry> leaves)
        {
            var latestLeaves = leaves
                .GroupBy(x => new PackageIdentity(x.Id, x.Version.ToNormalizedString()))
                .Select(x => x
                    .OrderByDescending(y => y.CommitTimeStamp)
                    .First())
                .ToList();

            var identityToPackageKey = await _packageService.AddOrUpdatePackagesAsync(latestLeaves);
            
            await _catalogService.AddOrUpdateAsync(page, leaves, identityToPackageKey);

            await _packageService.SetDeletedPackagesAsUnlistedInV2Async(latestLeaves.Where(x => x.IsDelete));
        }
    }
}

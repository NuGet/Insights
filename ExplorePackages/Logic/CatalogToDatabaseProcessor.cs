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
            var identityToPackageKey = await _packageService.AddOrUpdatePackagesAsync(leaves);
            
            await _catalogService.AddOrUpdateAsync(page, leaves, identityToPackageKey);

            var unavailableInV2 = (await TaskProcessor.ExecuteAsync(
                leaves.Where(x => x.IsDelete),
                async x =>
                {
                    var exists = await _v2Client.HasPackageAsync(
                        _settings.V2BaseUrl,
                        x.Id,
                        x.Version.ToNormalizedString(),
                        semVer2: true);

                    return exists ? x : null;
                })).Where(x => x != null).ToList();
            await _packageService.SetDeletedPackagesAsUnlistedInV2Async(unavailableInV2);
        }
    }
}

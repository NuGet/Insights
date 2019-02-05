using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class CatalogToDatabaseProcessor : ICatalogEntriesProcessor
    {
        private readonly IPackageService _packageService;
        private readonly CatalogClient _catalogClient;
        private readonly CatalogService _catalogService;
        private readonly V2Client _v2Client;

        public CatalogToDatabaseProcessor(
            IPackageService packageService,
            CatalogClient catalogClient,
            CatalogService catalogService,
            V2Client v2Client)
        {
            _packageService = packageService;
            _catalogClient = catalogClient;
            _catalogService = catalogService;
            _v2Client = v2Client;
        }

        public IReadOnlyList<string> DependencyCursorNames => new List<string>();

        public string CursorName => CursorNames.CatalogToDatabase;

        public async Task ProcessAsync(CatalogPageItem page, IReadOnlyList<CatalogLeafItem> leaves)
        {
            // Determine the listed status of all of the packages.
            var entryToListed = (await TaskProcessor.ExecuteAsync(
                leaves,
                async x =>
                {
                    bool listed;
                    if (x.IsPackageDelete())
                    {
                        listed = false;
                    }
                    else
                    {
                        listed = await IsListedAsync(x);
                    }

                    return KeyValuePairFactory.Create(x, listed);
                },
                workerCount: 32,
                token: CancellationToken.None))
                .ToDictionary(x => x.Key, x => x.Value, new CatalogLeafItemComparer());

            // Only add Package entities based on the latest commit timestamp.
            var latestLeaves = leaves
                .GroupBy(x => new PackageIdentity(x.PackageId, x.ParsePackageVersion().ToNormalizedString()))
                .Select(x => x
                    .OrderByDescending(y => y.CommitTimestamp)
                    .First())
                .ToList();

            var identityToPackageKey = await _packageService.AddOrUpdatePackagesAsync(latestLeaves, entryToListed);
            
            await _catalogService.AddOrUpdateAsync(page, leaves, identityToPackageKey, entryToListed);

            await _packageService.SetDeletedPackagesAsUnlistedInV2Async(latestLeaves);
        }

        private async Task<bool> IsListedAsync(CatalogLeafItem entry)
        {
            var leaf = (PackageDetailsCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(entry);
            return leaf.IsListed();
        }

        private class CatalogLeafItemComparer : IEqualityComparer<CatalogLeafItem>
        {
            public bool Equals(CatalogLeafItem x, CatalogLeafItem y)
            {
                return x?.Url == y?.Url;
            }

            public int GetHashCode(CatalogLeafItem obj)
            {
                return obj?.Url.GetHashCode() ?? 0;
            }
        }
    }
}

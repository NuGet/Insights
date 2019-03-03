using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Logic
{
    public class CatalogToDatabaseProcessor : ICatalogEntriesProcessor
    {
        private readonly IPackageService _packageService;
        private readonly CatalogClient _catalogClient;
        private readonly CatalogService _catalogService;
        private readonly V2Client _v2Client;
        private readonly IOptionsSnapshot<ExplorePackagesSettings> _options;

        public CatalogToDatabaseProcessor(
            IPackageService packageService,
            CatalogClient catalogClient,
            CatalogService catalogService,
            V2Client v2Client,
            IOptionsSnapshot<ExplorePackagesSettings> options)
        {
            _packageService = packageService;
            _catalogClient = catalogClient;
            _catalogService = catalogService;
            _v2Client = v2Client;
            _options = options;
        }

        public IReadOnlyList<string> DependencyCursorNames => new List<string>();

        public string CursorName => CursorNames.CatalogToDatabase;

        public async Task ProcessAsync(CatalogPageItem page, IReadOnlyList<CatalogLeafItem> leaves)
        {
            // Determine the listed status of all of the packages.
            var entryToVisibilityState = (await TaskProcessor.ExecuteAsync(
                leaves,
                async x =>
                {
                    PackageVisibilityState visiblityState;
                    if (x.IsPackageDelete())
                    {
                        visiblityState = new PackageVisibilityState(listed: false, semVerType: null);
                    }
                    else
                    {
                        visiblityState = await DetermineVisibilityStateAsync(x);
                    }

                    return KeyValuePairFactory.Create(x, visiblityState);
                },
                workerCount: _options.Value.WorkerCount,
                token: CancellationToken.None))
                .ToDictionary(x => x.Key, x => x.Value, new CatalogLeafItemComparer());

            // Group the leaves by package identity
            var groups = leaves
                .GroupBy(x => new PackageIdentity(x.PackageId, x.ParsePackageVersion().ToNormalizedString()));

            // Determine the first commit timestamp per latest catalog leaf.
            var latestEntryToFirstCommitTimestamp = groups
                .ToDictionary(
                    x => x.OrderByDescending(y => y.CommitTimestamp).First(),
                    x => x.Min(y => y.CommitTimestamp));

            // Determine the latest entries, sorted.
            var latestEntries = latestEntryToFirstCommitTimestamp
                .Keys
                .OrderBy(x => x.CommitTimestamp)
                .OrderBy(x => x.PackageId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.ParsePackageVersion())
                .ToList();

            // Add the CatalogPackage instances.
            var identityToPackageKey = await _packageService.AddOrUpdatePackagesAsync(
                latestEntries,
                latestEntryToFirstCommitTimestamp,
                entryToVisibilityState);
            
            // Add the CatalogPackageLeaf instances and their parent classes.
            await _catalogService.AddOrUpdateAsync(page, leaves, identityToPackageKey, entryToVisibilityState);

            // Consider deleted packages as unlisted in V2. This is so that listed status between V2 and the catalog is the same.
            await _packageService.SetDeletedPackagesAsUnlistedInV2Async(latestEntries);
        }

        private async Task<PackageVisibilityState> DetermineVisibilityStateAsync(CatalogLeafItem entry)
        {
            var leaf = (PackageDetailsCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(entry);

            var listed = leaf.IsListed();
            var semVerType = leaf.GetSemVerType();

            return new PackageVisibilityState(listed, semVerType);
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

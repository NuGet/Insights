using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
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
            // Determine the listed status of all of the packages.
            var entryToListed = (await TaskProcessor.ExecuteAsync(
                leaves,
                async x =>
                {
                    bool listed;
                    if (x.IsDelete)
                    {
                        listed = false;
                    }
                    else
                    {
                        listed = await IsListedAsync(x);
                    }

                    return KeyValuePairFactory.Create(x, listed);
                },
                workerCount: 16))
                .ToDictionary(x => x.Key, x => x.Value, new CatalogEntryComparer());

            // Only add Package entities based on the latest commit timestamp.
            var latestLeaves = leaves
                .GroupBy(x => new PackageIdentity(x.Id, x.Version.ToNormalizedString()))
                .Select(x => x
                    .OrderByDescending(y => y.CommitTimeStamp)
                    .First())
                .ToList();

            var identityToPackageKey = await _packageService.AddOrUpdatePackagesAsync(latestLeaves, entryToListed);
            
            await _catalogService.AddOrUpdateAsync(page, leaves, identityToPackageKey, entryToListed);

            await _packageService.SetDeletedPackagesAsUnlistedInV2Async(latestLeaves.Where(x => x.IsDelete));
        }

        private async Task<bool> IsListedAsync(CatalogEntry entry)
        {
            var details = await entry.GetPackageDetailsAsync();

            var listedProperty = details.Property("listed");
            if (listedProperty != null)
            {
                return (bool)listedProperty.Value;
            }

            var publishedProperty = details.Property("published");
            var published = DateTimeOffset.Parse(
                (string)publishedProperty.Value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal);

            return published.ToUniversalTime().Year != 1900;
        }

        private class CatalogEntryComparer : IEqualityComparer<CatalogEntry>
        {
            public bool Equals(CatalogEntry x, CatalogEntry y)
            {
                return x?.Uri == y?.Uri;
            }

            public int GetHashCode(CatalogEntry obj)
            {
                return obj?.Uri.GetHashCode() ?? 0;
            }
        }
    }
}

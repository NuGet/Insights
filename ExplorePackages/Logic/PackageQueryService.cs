using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Microsoft.EntityFrameworkCore;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageQueryService
    {
        private const int PageSize = 1000;
        private readonly PackageService _packageService;
        private readonly PackageCommitEnumerator _enumerator;
        private readonly ILogger _log;

        public PackageQueryService(PackageService packageService, PackageCommitEnumerator enumerator, ILogger log)
        {
            _packageService = packageService;
            _enumerator = enumerator;
            _log = log;
        }

        public async Task AddQueryAsync(string queryName, string cursorName)
        {
            using (var entityContext = new EntityContext())
            {
                var query = await GetQueryAsync(queryName, entityContext);

                if (query == null)
                {
                    var cursorService = new CursorService();
                    await cursorService.EnsureExistsAsync(cursorName);
                    var cursor = await cursorService.GetAsync(cursorName);
                    
                    query = new PackageQueryEntity
                    {
                        Name = queryName,
                        CursorKey = cursor.CursorKey,
                    };

                    entityContext.PackageQueries.Add(query);

                    await entityContext.SaveChangesAsync();
                }
                else if (query.Cursor.Name != cursorName)
                {
                    throw new ArgumentException($"The query {queryName} is not using cursor {cursorName}.");
                }
            }
        }

        public Task<IReadOnlyList<PackageCommit>> GetMatchedPackageCommitsAsync(DateTimeOffset start, DateTimeOffset end, IReadOnlyList<string> queryNames)
        {
            return _enumerator.GetPackageCommitsAsync(
                e => e
                    .Packages
                    .Where(x => x
                        .PackageQueryMatches
                        .Any(pqm => queryNames.Contains(pqm.PackageQuery.Name))),
                start,
                end);
        }

        private static async Task<PackageQueryEntity> GetQueryAsync(string queryName, EntityContext entityContext)
        {
            return await entityContext
                .PackageQueries
                .Include(x => x.Cursor)
                .Where(x => x.Name == queryName)
                .FirstOrDefaultAsync();
        }

        public async Task<PackageQueryMatches> GetMatchedPackagesAsync(string queryName, long lastKey)
        {
            using (var entityContext = new EntityContext())
            {
                var matches = await entityContext
                    .PackageQueryMatches
                    .Include(x => x.Package.CatalogPackage)
                    .Where(x => x.PackageQuery.Name == queryName && x.PackageQueryMatchKey > lastKey)
                    .OrderBy(x => x.PackageQueryMatchKey)
                    .Take(PageSize)
                    .Select(x => new { x.PackageQueryMatchKey, x.Package })
                    .ToListAsync();

                if (!matches.Any())
                {
                    return new PackageQueryMatches(0, new List<PackageEntity>());
                }
                
                return new PackageQueryMatches(
                    matches.Max(x => x.PackageQueryMatchKey),
                    matches.Select(x => x.Package).ToList());
            }
        }

        public async Task RemoveMatchesAsync(string queryName, IReadOnlyList<PackageIdentity> identities)
        {
            using (var entityContext = new EntityContext())
            {
                var query = await GetQueryAsync(queryName, entityContext);
                if (query == null)
                {
                    return;
                }

                var existingMatches = await GetExistingMatchesAsync(entityContext, query, identities);

                entityContext.PackageQueryMatches.RemoveRange(existingMatches);

                await entityContext.SaveChangesAsync();
            }
        }

        public async Task AddMatchesAsync(string queryName, IReadOnlyList<PackageIdentity> identities)
        {
            using (var entityContext = new EntityContext())
            {
                // Find the query.
                var query = await GetQueryAsync(queryName, entityContext);
                if (query == null)
                {
                    throw new ArgumentException($"The query {queryName} does not exist.");
                }

                // Don't persist matches that already exist.
                var existingMatches = await GetExistingMatchesAsync(entityContext, query, identities);
                var existingIdentities = existingMatches
                    .Select(x => new PackageIdentity(x.Package.PackageRegistration.Id, x.Package.Version))
                    .ToList();
                var newIdentities = identities
                    .Except(existingIdentities)
                    .ToList();

                // Find the packages for the existing matches.
                var packages = await GetPackagesAsync(newIdentities);

                // Add the new matches.
                var newMatches = packages
                    .Select(x => new PackageQueryMatchEntity
                    {
                        PackageQueryKey = query.PackageQueryKey,
                        PackageKey = x.PackageKey,
                    });
                await entityContext.PackageQueryMatches.AddRangeAsync(newMatches);

                await entityContext.SaveChangesAsync();
            }
        }

        private async Task<IReadOnlyList<PackageEntity>> GetPackagesAsync(IReadOnlyList<PackageIdentity> identities)
        {
            var packages = await _packageService.GetBatchAsync(identities);

            var missing = identities
                .Except(packages.Select(x => new PackageIdentity(x.PackageRegistration.Id, x.Version)))
                .ToList();
            if (missing.Any())
            {
                throw new ArgumentException($"The following packages do not exist: {string.Join(", ", missing)}");
            }

            return packages;
        }

        private static async Task<List<PackageQueryMatchEntity>> GetExistingMatchesAsync(EntityContext entityContext, PackageQueryEntity query, IReadOnlyList<PackageIdentity> identities)
        {
            var identityStrings = new HashSet<string>(identities.Select(x => x.Value));

            var queryKey = query.PackageQueryKey;

            var existingIdentities = await entityContext
                .PackageQueryMatches
                .Include(x => x.Package)
                .Include(x => x.Package.PackageRegistration)
                .Where(x => identityStrings.Contains(x.Package.Identity) && x.PackageQueryKey == queryKey)
                .ToListAsync();

            return existingIdentities;
        }
    }
}

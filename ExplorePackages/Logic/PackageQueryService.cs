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
        private readonly ILogger _log;

        public PackageQueryService(ILogger log)
        {
            _packageService = new PackageService(log);
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

                    query = new PackageQuery
                    {
                        Name = queryName,
                        CursorName = cursorName,
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

        private static async Task<PackageQuery> GetQueryAsync(string queryName, EntityContext entityContext)
        {
            return await entityContext
                .PackageQueries
                .Include(x => x.Cursor)
                .Where(x => x.Name == queryName)
                .FirstOrDefaultAsync();
        }

        public async Task DeleteResultsForPackagesAsync(IReadOnlyList<int> packageKeys)
        {
            using (var entityContext = new EntityContext())
            {
                var matches = await entityContext
                    .PackageQueryMatches
                    .Where(x => packageKeys.Contains(x.PackageKey))
                    .ToListAsync();

                entityContext
                    .PackageQueryMatches
                    .RemoveRange(matches);

                var deleted = await entityContext.SaveChangesAsync();

                _log.LogInformation($"Deleted {deleted} query matches (looked for {packageKeys.Count} packages).");
            }
        }

        public async Task<PackageQueryMatches> GetMatchedPackagesAsync(string queryName, long lastKey)
        {
            using (var entityContext = new EntityContext())
            {
                var matches = await entityContext
                    .PackageQueryMatches
                    .Where(x => x.PackageQuery.Name == queryName && x.Key > lastKey)
                    .OrderBy(x => x.Key)
                    .Take(PageSize)
                    .Select(x => new { x.Key, x.Package })
                    .ToListAsync();

                if (!matches.Any())
                {
                    return new PackageQueryMatches(0, new List<Package>());
                }
                
                return new PackageQueryMatches(
                    matches.Max(x => x.Key),
                    matches.Select(x => x.Package).ToList());
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
                var identityStrings = new HashSet<string>(identities
                    .Select(x => $"{x.Id}/{x.Version}"));
                var queryKey = query.Key;
                var existingIdentities = (await entityContext
                    .PackageQueryMatches
                    .Where(x => identityStrings.Contains(x.Package.Identity) && x.PackageQueryKey == queryKey)
                    .Select(x => new { x.Package.Id, x.Package.Version })
                    .ToListAsync())
                    .Select(x => new PackageIdentity(x.Id, x.Version))
                    .ToList();
                var newIdentities = identities
                    .Except(existingIdentities)
                    .ToList();

                // Find the packages for new matches.
                var packages = await _packageService.GetBatchAsync(newIdentities);
                var missing = newIdentities
                    .Except(packages.Select(x => new PackageIdentity(x.Id, x.Version)))
                    .ToList();
                if (missing.Any())
                {
                    throw new ArgumentException($"The following packages do not exist: {string.Join(", ", missing)}");
                }

                // Add the new matches.
                var newMatches = packages
                    .Select(x => new PackageQueryMatch
                    {
                        PackageQueryKey = query.Key,
                        PackageKey = x.Key,
                    });
                await entityContext.PackageQueryMatches.AddRangeAsync(newMatches);

                await entityContext.SaveChangesAsync();
            }
        }
    }
}

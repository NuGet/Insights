using System;
using System.Collections;
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

                await entityContext.SaveChangesAsync();
            }
        }

        public async Task AddMatchAsync(string queryName, string id, string version)
        {
            using (var entityContext = new EntityContext())
            {
                var query = await GetQueryAsync(queryName, entityContext);

                if (query == null)
                {
                    throw new ArgumentException($"The query {queryName} does not exist.");
                }

                var package = await _packageService.GetAsync(id, version);

                if (package == null)
                {
                    throw new ArgumentException($"The package {id} {version} does not exist.");
                }

                entityContext
                    .PackageQueryMatches
                    .Add(new PackageQueryMatch
                    {
                        PackageQueryKey = query.Key,
                        PackageKey = package.Key,
                    });

                await entityContext.SaveChangesAsync();
            }
        }
    }
}

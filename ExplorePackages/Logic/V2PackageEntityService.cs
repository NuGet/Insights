using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Microsoft.EntityFrameworkCore;
using NuGet.Common;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Logic
{
    public class V2PackageEntityService
    {
        private readonly ILogger _log;

        public V2PackageEntityService(ILogger log)
        {
            _log = log;
        }

        /// <summary>
        /// Adds the provided catalog entries to the database. Catalog entries are processed in the order provided.
        /// </summary>
        public async Task<int> AddOrUpdatePackagesAsync(IEnumerable<V2Package> packages)
        {
            using (var entityContext = new EntityContext())
            {
                var entities = new List<V2PackageEntity>();
                var identityToLatest = new Dictionary<string, V2PackageEntity>(StringComparer.OrdinalIgnoreCase);
                foreach (var package in packages)
                {
                    var entity = new V2PackageEntity
                    {
                        Id = package.Id,
                        Version = NuGetVersion.Parse(package.Version).ToNormalizedString(),
                        Created = package.Created.UtcTicks,
                    };

                    entity.Identity = $"{entity.Id}/{entity.Version}";
                    identityToLatest[entity.Identity] = entity;

                    entities.Add(entity);
                }

                var getExistingStopwatch = Stopwatch.StartNew();
                var identities = entities.Select(x => x.Identity).ToList();
                var existingPackages = await entityContext
                    .V2PackageEntities
                    .Where(p => identities.Contains(p.Identity))
                    .ToListAsync();

                _log.LogInformation($"Got {existingPackages.Count} existing. {getExistingStopwatch.ElapsedMilliseconds}ms");

                // Update existing records.
                foreach (var existingPackage in existingPackages)
                {
                    var latestPackage = identityToLatest[existingPackage.Identity];
                    identityToLatest.Remove(existingPackage.Identity);

                    existingPackage.Created = latestPackage.Created;
                }

                // Add new records.
                await entityContext.V2PackageEntities.AddRangeAsync(identityToLatest.Values);

                var commitStopwatch = Stopwatch.StartNew();
                var changes = await entityContext.SaveChangesAsync();
                _log.LogInformation($"Committed {changes} changes. {commitStopwatch.ElapsedMilliseconds}ms");

                return identityToLatest.Count;
            }
        }
    }
}

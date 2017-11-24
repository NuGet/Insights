using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Microsoft.EntityFrameworkCore;
using NuGet.CatalogReader;
using NuGet.Common;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageService
    {
        private readonly PackageCommitEnumerator _enumerator;
        private readonly ILogger _log;

        public PackageService(PackageCommitEnumerator enumerator, ILogger log)
        {
            _enumerator = enumerator;
            _log = log;
        }

        public async Task<PackageEntity> GetPackageAsync(string id, string version)
        {
            var normalizedVersion = NuGetVersion.Parse(version).ToNormalizedString();
            using (var entityContext = new EntityContext())
            {
                return await entityContext
                    .Packages
                    .Include(x => x.PackageRegistration)
                    .Where(x => x.PackageRegistration.Id == id && x.Version == normalizedVersion)
                    .FirstOrDefaultAsync();
            }
        }

        public async Task<IReadOnlyList<PackageEntity>> GetBatchAsync(IReadOnlyList<PackageIdentity> identities)
        {
            using (var entityContext = new EntityContext())
            {
                var identityStrings = identities
                    .Select(x => $"{x.Id}/{x.Version}")
                    .ToList();

                return await entityContext
                    .Packages
                    .Include(x => x.PackageRegistration)
                    .Where(x => identityStrings.Contains(x.Identity))
                    .ToListAsync();
            }
        }

        public Task<IReadOnlyList<PackageCommit>> GetPackageCommitsAsync(DateTimeOffset start, DateTimeOffset end)
        {
            return _enumerator.GetPackageCommitsAsync(
                e => e.Packages,
                start,
                end);
        }

        private async Task<IReadOnlyDictionary<string, PackageRegistrationEntity>> AddPackageRegistrationsAsync(
            EntityContext entityContext,
            IEnumerable<string> ids)
        {
            var idSet = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);

            var existingRegistrations = await entityContext
                .PackageRegistrations
                .Where(x => idSet.Contains(x.Id))
                .ToListAsync();

            idSet.ExceptWith(existingRegistrations.Select(x => x.Id));

            var newRegistrations = idSet
                .Select(x => new PackageRegistrationEntity { Id = x })
                .ToList();

            await entityContext.AddRangeAsync(newRegistrations);

            return existingRegistrations
                .Concat(newRegistrations)
                .ToDictionary(x => x.Id, x => x, StringComparer.OrdinalIgnoreCase);
        }

        private async Task AddOrUpdatePackagesAsync<T, TKey>(
            IEnumerable<T> foreignPackages,
            Func<T, TKey> orderBy,
            Func<T, string> getId,
            Func<T, string> getVersion,
            Action<PackageEntity, T> initializePackageFromForeign,
            Action<PackageEntity, T> updatePackageFromForeign,
            Action<PackageEntity, PackageEntity> updateExistingPackage)
        {
            using (var entityContext = new EntityContext())
            {
                var packageRegistrations = await AddPackageRegistrationsAsync(
                    entityContext,
                    foreignPackages.Select(getId));

                // Create a mapping from package identity to latest package.
                var identityToLatest = new Dictionary<string, PackageEntity>(StringComparer.OrdinalIgnoreCase);
                foreach (var foreignPackage in foreignPackages)
                {
                    var id = getId(foreignPackage);
                    var version = NuGetVersion.Parse(getVersion(foreignPackage)).ToNormalizedString();
                    var identity = $"{id}/{version}";

                    PackageEntity latestPackage;
                    if (!identityToLatest.TryGetValue(identity, out latestPackage))
                    {
                        latestPackage = new PackageEntity
                        {
                            PackageRegistration = packageRegistrations[id],
                            Version = version,
                            Identity = identity,
                        };

                        initializePackageFromForeign(latestPackage, foreignPackage);

                        identityToLatest[latestPackage.Identity] = latestPackage;
                    }
                    else
                    {
                        updatePackageFromForeign(latestPackage, foreignPackage);
                    }
                }

                var getExistingStopwatch = Stopwatch.StartNew();
                var identities = identityToLatest.Keys.ToList();
                var existingPackages = await entityContext
                    .Packages
                    .Include(x => x.V2Package)
                    .Include(x => x.CatalogPackage)
                    .Include(x => x.PackageDownloads)
                    .Where(p => identities.Contains(p.Identity))
                    .ToListAsync();

                _log.LogInformation($"Got {existingPackages.Count} existing. {getExistingStopwatch.ElapsedMilliseconds}ms");

                // Update existing records.
                foreach (var existingPackage in existingPackages)
                {
                    var latestPackage = identityToLatest[existingPackage.Identity];
                    identityToLatest.Remove(existingPackage.Identity);

                    updateExistingPackage(existingPackage, latestPackage);
                }

                // Add new records.
                await entityContext.Packages.AddRangeAsync(identityToLatest.Values);

                // Commit the changes.
                var commitStopwatch = Stopwatch.StartNew();
                var changes = await entityContext.SaveChangesAsync();
                _log.LogInformation($"Committed {changes} changes. {commitStopwatch.ElapsedMilliseconds}ms");
            }
        }

        public async Task AddOrUpdatePackagesAsync(IEnumerable<V2Package> v2Packages)
        {
            await AddOrUpdatePackagesAsync(
                v2Packages,
                v2 => v2.Created,
                v2 => v2.Id,
                v2 => v2.Version,
                (p, v2) => p.V2Package = new V2PackageEntity
                {
                    CreatedTimestamp = v2.Created.UtcTicks
                },
                (p, v2) => p.V2Package.CreatedTimestamp = Math.Min(
                    p.V2Package.CreatedTimestamp,
                    v2.Created.UtcTicks),
                (pe, pl) =>
                {
                    if (pe.V2Package == null)
                    {
                        pe.V2Package = pl.V2Package;
                    }
                    else
                    {
                        pe.V2Package.CreatedTimestamp = Math.Max(
                            pe.V2Package.CreatedTimestamp,
                            pl.V2Package.CreatedTimestamp);
                    }
                });
        }

        /// <summary>
        /// Adds the provided catalog entries to the database. Catalog entries are processed in the order provided.
        /// </summary>
        public async Task AddOrUpdatePackagesAsync(IEnumerable<CatalogEntry> entries)
        {
            await AddOrUpdatePackagesAsync(
                entries,
                c => c.CommitTimeStamp,
                c => c.Id,
                c => c.Version.ToNormalizedString(),
                (p, c) => p.CatalogPackage = new CatalogPackageEntity
                {
                    Deleted = c.IsDelete,
                    FirstCommitTimestamp = c.CommitTimeStamp.UtcTicks,
                    LastCommitTimestamp = c.CommitTimeStamp.UtcTicks,
                },
                (p, c) =>
                {
                    p.CatalogPackage.Deleted = c.IsDelete;

                    p.CatalogPackage.FirstCommitTimestamp = Math.Min(
                        p.CatalogPackage.FirstCommitTimestamp,
                        c.CommitTimeStamp.UtcTicks);

                    p.CatalogPackage.LastCommitTimestamp = Math.Max(
                        p.CatalogPackage.LastCommitTimestamp,
                        c.CommitTimeStamp.UtcTicks);
                },
                (pe, pl) =>
                {
                    if (pe.CatalogPackage == null)
                    {
                        pe.CatalogPackage = pl.CatalogPackage;
                    }
                    else
                    {
                        pe.CatalogPackage.Deleted = pl.CatalogPackage.Deleted;

                        pe.CatalogPackage.FirstCommitTimestamp = Math.Min(
                            pe.CatalogPackage.FirstCommitTimestamp,
                            pl.CatalogPackage.FirstCommitTimestamp);

                        pe.CatalogPackage.LastCommitTimestamp = Math.Max(
                            pe.CatalogPackage.LastCommitTimestamp,
                            pl.CatalogPackage.LastCommitTimestamp);
                    }
                });
        }
    }
}

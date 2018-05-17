using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Knapcode.ExplorePackages.Entities;
using Knapcode.MiniZip;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NuGet.CatalogReader;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageService : IPackageService
    {
        private static readonly IMapper Mapper;
        private readonly PackageCommitEnumerator _enumerator;
        private readonly ILogger<PackageService> _logger;

        static PackageService()
        {
            var mapperConfiguration = new MapperConfiguration(expression =>
            {
                expression
                    .CreateMap<PackageArchiveEntity, PackageArchiveEntity>()
                    .ForMember(x => x.PackageKey, x => x.Ignore())
                    .ForMember(x => x.Package, x => x.Ignore())
                    .ForMember(x => x.PackageEntries, x => x.Ignore());

                expression
                    .CreateMap<PackageEntryEntity, PackageEntryEntity>()
                    .ForMember(x => x.PackageEntryKey, x => x.Ignore())
                    .ForMember(x => x.PackageKey, x => x.Ignore())
                    .ForMember(x => x.PackageArchive, x => x.Ignore())
                    .ForMember(x => x.Index, x => x.Ignore());
            });

            Mapper = mapperConfiguration.CreateMapper();
        }

        public PackageService(PackageCommitEnumerator enumerator, ILogger<PackageService> logger)
        {
            _enumerator = enumerator;
            _logger = logger;
        }

        public async Task<IReadOnlyDictionary<string, PackageRegistrationEntity>> AddPackageRegistrationsAsync(
            IEnumerable<string> ids,
            bool includePackages)
        {
            using (var entityContext = new EntityContext())
            {
                var registrations = await AddPackageRegistrationsAsync(
                    entityContext,
                    ids,
                    includePackages);

                await entityContext.SaveChangesAsync();

                return registrations;
            }
        }

        public async Task<PackageEntity> GetPackageOrNullAsync(string id, string version)
        {
            var normalizedVersion = NuGetVersion.Parse(version).ToNormalizedString();
            using (var entityContext = new EntityContext())
            {
                return await entityContext
                    .Packages
                    .Include(x => x.PackageRegistration)
                    .Include(x => x.V2Package)
                    .Include(x => x.CatalogPackage)
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

        private async Task<IReadOnlyDictionary<string, PackageRegistrationEntity>> AddPackageRegistrationsAsync(
            EntityContext entityContext,
            IEnumerable<string> ids,
            bool includePackages)
        {
            var idSet = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);

            IQueryable<PackageRegistrationEntity> existingRegistrationsQueryable = entityContext
                .PackageRegistrations;

            if (includePackages)
            {
                existingRegistrationsQueryable = existingRegistrationsQueryable
                    .Include(x => x.Packages)
                    .ThenInclude(x => x.CatalogPackage);
            }

            var existingRegistrations = await existingRegistrationsQueryable
                .Where(x => idSet.Contains(x.Id))
                .ToListAsync();

            idSet.ExceptWith(existingRegistrations.Select(x => x.Id));

            var newRegistrations = idSet
                .Select(x => new PackageRegistrationEntity
                {
                    Id = x,
                    Packages = new List<PackageEntity>(),
                })
                .ToList();

            await entityContext.AddRangeAsync(newRegistrations);

            return existingRegistrations
                .Concat(newRegistrations)
                .ToDictionary(x => x.Id, x => x, StringComparer.OrdinalIgnoreCase);
        }

        private async Task<IReadOnlyDictionary<string, long>>  AddOrUpdatePackagesAsync<T>(
            QueryEntities<PackageEntity> getPackages,
            IEnumerable<T> foreignPackages,
            Func<IEnumerable<T>, IEnumerable<T>> sort,
            Func<T, string> getId,
            Func<T, string> getVersion,
            Func<PackageEntity, T, Task> initializePackageFromForeignAsync,
            Func<PackageEntity, T, Task> updatePackageFromForeignAsync,
            Action<EntityContext, PackageEntity, PackageEntity> updateExistingPackage)
        {
            using (var entityContext = new EntityContext())
            {
                var packageRegistrations = await AddPackageRegistrationsAsync(
                    entityContext,
                    foreignPackages.Select(getId),
                    includePackages: false);

                // Create a mapping from package identity to latest package.
                var identityToLatest = new Dictionary<string, PackageEntity>(StringComparer.OrdinalIgnoreCase);
                foreach (var foreignPackage in foreignPackages)
                {
                    var id = getId(foreignPackage);
                    var version = NuGetVersion.Parse(getVersion(foreignPackage)).ToNormalizedString();
                    var identity = $"{id}/{version}";
                    
                    if (!identityToLatest.TryGetValue(identity, out var latestPackage))
                    {
                        latestPackage = new PackageEntity
                        {
                            PackageRegistration = packageRegistrations[id],
                            Version = version,
                            Identity = identity,
                        };

                        await initializePackageFromForeignAsync(latestPackage, foreignPackage);

                        identityToLatest[latestPackage.Identity] = latestPackage;
                    }
                    else
                    {
                        await updatePackageFromForeignAsync(latestPackage, foreignPackage);
                    }
                }

                var getExistingStopwatch = Stopwatch.StartNew();
                var identities = identityToLatest.Keys.ToList();

                var existingPackages = await getPackages(entityContext)
                    .Where(p => identities.Contains(p.Identity))
                    .ToListAsync();

                _logger.LogInformation(
                    "Got {ExistingCount} existing. {ElapsedMilliseconds}ms",
                    existingPackages.Count,
                    getExistingStopwatch.ElapsedMilliseconds);

                // Keep track of the resulting package keys.
                var identityToPackageKey = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

                // Update existing records.
                foreach (var existingPackage in existingPackages)
                {
                    var latestPackage = identityToLatest[existingPackage.Identity];
                    identityToLatest.Remove(existingPackage.Identity);

                    updateExistingPackage(entityContext, existingPackage, latestPackage);

                    identityToPackageKey.Add(existingPackage.Identity, existingPackage.PackageKey);
                }

                // Add new records.
                await entityContext.Packages.AddRangeAsync(identityToLatest.Values);

                // Commit the changes.
                var commitStopwatch = Stopwatch.StartNew();
                var changes = await entityContext.SaveChangesAsync();
                _logger.LogInformation(
                    "Committed {Changes} changes. {ElapsedMilliseconds}ms",
                    changes,
                    commitStopwatch.ElapsedMilliseconds);

                // Add the new package keys.
                foreach (var pair in identityToLatest)
                {
                    identityToPackageKey.Add(pair.Key, pair.Value.PackageKey);
                }

                return identityToPackageKey;
            }
        }

        public async Task AddOrUpdatePackagesAsync(IEnumerable<PackageArchiveMetadata> metadataSequence)
        {
            await AddOrUpdatePackagesAsync(
                x => x
                    .Packages
                    .Include(y => y.PackageArchive)
                    .ThenInclude(y => y.PackageEntries),
                metadataSequence,
                x => x,
                d => d.Id,
                d => d.Version,
                (p, f) =>
                {
                    p.PackageArchive = Initialize(new PackageArchiveEntity(), f);
                    return Task.CompletedTask;
                },
                (p, f) =>
                {
                    Initialize(p.PackageArchive, f);
                    return Task.CompletedTask;
                },
                (c, pe, pl) =>
                {
                    if (pe.PackageArchive == null)
                    {
                        pe.PackageArchive = pl.PackageArchive;
                    }
                    else
                    {
                        Update(c, pe.PackageArchive, pl.PackageArchive);
                    }
                });
        }

        private void Update(EntityContext entityContext, PackageArchiveEntity existing, PackageArchiveEntity latest)
        {
            Mapper.Map(latest, existing);

            latest.PackageEntries.Sort((a, b) => a.Index.CompareTo(b.Index));
            existing.PackageEntries.Sort((a, b) => a.Index.CompareTo(b.Index));
            
            for (var index = 0; index < latest.PackageEntries.Count; index++)
            {
                var latestEntry = latest.PackageEntries[index];

                if (index < existing.PackageEntries.Count)
                {
                    var existingEntry = existing.PackageEntries[index];

                    if (existingEntry.Index != (ulong)index)
                    {
                        throw new InvalidOperationException("One of the package archive entries seems to have an incorrect index.");
                    }

                    Mapper.Map(latestEntry, existingEntry);
                }
                else if (index == existing.PackageEntries.Count)
                {
                    latestEntry.PackageKey = existing.PackageKey;
                    latestEntry.PackageArchive = existing;

                    existing.PackageEntries.Add(latestEntry);

                    entityContext.PackageEntries.Add(latestEntry);
                }
                else
                {
                    throw new InvalidOperationException("The list of existing package entries should have grown.");
                }
            }

            while (latest.PackageEntries.Count < existing.PackageEntries.Count)
            {
                var last = existing.PackageEntries.Last();
                existing.PackageEntries.RemoveAt(existing.PackageEntries.Count - 1);
                entityContext.PackageEntries.Remove(last);
            }
        }

        private PackageArchiveEntity Initialize(PackageArchiveEntity entity, PackageArchiveMetadata metadata)
        {
            entity.Size = metadata.Size;
            entity.EntryCount = metadata.ZipDirectory.Entries.Count;

            entity.CentralDirectorySize = metadata.ZipDirectory.CentralDirectorySize;
            entity.Comment = metadata.ZipDirectory.Comment;
            entity.CommentSize = metadata.ZipDirectory.CommentSize;
            entity.DiskWithStartOfCentralDirectory = metadata.ZipDirectory.DiskWithStartOfCentralDirectory;
            entity.EntriesForWholeCentralDirectory = metadata.ZipDirectory.EntriesForWholeCentralDirectory;
            entity.EntriesInThisDisk = metadata.ZipDirectory.EntriesInThisDisk;
            entity.NumberOfThisDisk = metadata.ZipDirectory.NumberOfThisDisk;
            entity.OffsetAfterEndOfCentralDirectory = metadata.ZipDirectory.OffsetAfterEndOfCentralDirectory;
            entity.OffsetOfCentralDirectory = metadata.ZipDirectory.OffsetOfCentralDirectory;

            entity.Zip64CentralDirectorySize = metadata.ZipDirectory.Zip64?.CentralDirectorySize;
            entity.Zip64DiskWithStartOfCentralDirectory = metadata.ZipDirectory.Zip64?.DiskWithStartOfCentralDirectory;
            entity.Zip64DiskWithStartOfEndOfCentralDirectory = metadata.ZipDirectory.Zip64?.DiskWithStartOfEndOfCentralDirectory;
            entity.Zip64EndOfCentralDirectoryOffset = metadata.ZipDirectory.Zip64?.EndOfCentralDirectoryOffset;
            entity.Zip64EntriesForWholeCentralDirectory = metadata.ZipDirectory.Zip64?.EntriesForWholeCentralDirectory;
            entity.Zip64EntriesInThisDisk = metadata.ZipDirectory.Zip64?.EntriesInThisDisk;
            entity.Zip64NumberOfThisDisk = metadata.ZipDirectory.Zip64?.NumberOfThisDisk;
            entity.Zip64OffsetAfterEndOfCentralDirectoryLocator = metadata.ZipDirectory.Zip64?.OffsetAfterEndOfCentralDirectoryLocator;
            entity.Zip64OffsetOfCentralDirectory = metadata.ZipDirectory.Zip64?.OffsetOfCentralDirectory;
            entity.Zip64SizeOfCentralDirectoryRecord = metadata.ZipDirectory.Zip64?.SizeOfCentralDirectoryRecord;
            entity.Zip64TotalNumberOfDisks = metadata.ZipDirectory.Zip64?.TotalNumberOfDisks;
            entity.Zip64VersionMadeBy = metadata.ZipDirectory.Zip64?.VersionMadeBy;
            entity.Zip64VersionToExtract = metadata.ZipDirectory.Zip64?.VersionToExtract;

            // Make sure the entries are sorted by index.
            if (entity.PackageEntries == null)
            {
                entity.PackageEntries = new List<PackageEntryEntity>();
            }

            entity.PackageEntries.Sort((a, b) => a.Index.CompareTo(b.Index));
            
            for (var index = 0; index < metadata.ZipDirectory.Entries.Count; index++)
            {
                var entryMetadata = metadata.ZipDirectory.Entries[index];
                PackageEntryEntity entryEntity;
                if (index < entity.PackageEntries.Count)
                {
                    entryEntity = entity.PackageEntries[index];

                    if (entryEntity.Index != (ulong)index)
                    {
                        throw new InvalidOperationException("One of the package archive entries seems to have an incorrect index.");
                    }
                }
                else if(index == entity.PackageEntries.Count)
                {
                    entryEntity = new PackageEntryEntity
                    {
                        PackageArchive = entity,
                        Index = (ulong)index,
                    };

                    entity.PackageEntries.Add(entryEntity);
                }
                else
                {
                    throw new InvalidOperationException("The list of existing package entries should have grown.");
                }

                Initialize(entryEntity, entryMetadata);
            }

            entity
                .PackageEntries
                .RemoveAll(x => x.Index >= (ulong)metadata.ZipDirectory.Entries.Count);

            return entity;
        }

        private PackageEntryEntity Initialize(PackageEntryEntity entity, CentralDirectoryHeader metadata)
        {
            entity.Comment = metadata.Comment;
            entity.CommentSize = metadata.CommentSize;
            entity.CompressedSize = metadata.CompressedSize;
            entity.CompressionMethod = metadata.CompressionMethod;
            entity.Crc32 = metadata.Crc32;
            entity.DiskNumberStart = metadata.DiskNumberStart;
            entity.ExternalAttributes = metadata.ExternalAttributes;
            entity.ExtraField = metadata.ExtraField;
            entity.ExtraFieldSize = metadata.ExtraFieldSize;
            entity.Flags = metadata.Flags;
            entity.InternalAttributes = metadata.InternalAttributes;
            entity.LastModifiedDate = metadata.LastModifiedDate;
            entity.LastModifiedTime = metadata.LastModifiedTime;
            entity.LocalHeaderOffset = metadata.LocalHeaderOffset;
            entity.Name = metadata.Name;
            entity.NameSize = metadata.NameSize;
            entity.UncompressedSize = metadata.UncompressedSize;
            entity.VersionMadeBy = metadata.VersionMadeBy;
            entity.VersionToExtract = metadata.VersionToExtract;

            return entity;
        }

        public async Task AddOrUpdatePackagesAsync(IEnumerable<PackageDownloads> packageDownloads)
        {
            var identityToPackageDownloads = packageDownloads
                .GroupBy(x => new PackageIdentity(x.Id, x.Version))
                .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.Downloads).First());

            var identityToPackageKey = await AddOrUpdatePackagesAsync(identityToPackageDownloads.Keys);

            var packageKeyDownloadPairs = identityToPackageDownloads
                .Select(x => KeyValuePair.Create(identityToPackageKey[x.Key.Value], x.Value.Downloads))
                .OrderBy(x => x.Key)
                .ToList();

            var changeCount = 0;
            var stopwatch = Stopwatch.StartNew();

            using (var entityContext = new EntityContext())
            using (var connection = entityContext.Database.GetDbConnection())
            {
                await connection.OpenAsync();

                using (var transaction = connection.BeginTransaction())
                {
                    var command = connection.CreateCommand();

                    command.CommandText = @"
                        INSERT OR REPLACE INTO PackageDownloads (PackageKey, Downloads)
                        VALUES (@PackageKey, @Downloads)";

                    var packageKeyParameter = command.CreateParameter();
                    packageKeyParameter.ParameterName = "PackageKey";
                    packageKeyParameter.DbType = DbType.Int64;
                    command.Parameters.Add(packageKeyParameter);

                    var downloadsParameter = command.CreateParameter();
                    downloadsParameter.ParameterName = "Downloads";
                    downloadsParameter.DbType = DbType.Int64;
                    command.Parameters.Add(downloadsParameter);

                    foreach (var pair in packageKeyDownloadPairs)
                    {
                        packageKeyParameter.Value = pair.Key;
                        downloadsParameter.Value = pair.Value;
                        changeCount += command.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
            }

            _logger.LogInformation(
                "Committed {ChangeCount} changes. {ElapsedMilliseconds}ms.",
                changeCount,
                stopwatch.ElapsedMilliseconds);
        }

        public async Task<IReadOnlyDictionary<string, long>> AddOrUpdatePackagesAsync(IEnumerable<PackageIdentity> identities)
        {
            return await AddOrUpdatePackagesAsync(
                x => x.Packages,
                identities,
                x => x.OrderBy(y => y.Id).ThenBy(y => y.Version),
                x => x.Id,
                x => x.Version,
                (p, i) =>
                {
                    return Task.CompletedTask;
                },
                (p, i) =>
                {
                    return Task.CompletedTask;
                },
                (c, pe, pl) => {});
        }

        public async Task AddOrUpdatePackagesAsync(IEnumerable<V2Package> v2Packages)
        {
            await AddOrUpdatePackagesAsync(
                x => x
                    .Packages
                    .Include(y => y.V2Package),
                v2Packages,
                x => x.OrderBy(v2 => v2.Created),
                v2 => v2.Id,
                v2 => v2.Version,
                (p, v2) =>
                {
                    p.V2Package = ToEntity(v2);
                    return Task.CompletedTask;
                },
                (p, v2) =>
                {
                    var e = ToEntity(v2);
                    if (e.LastUpdatedTimestamp < p.V2Package.LastUpdatedTimestamp && false)
                    {
                        return Task.CompletedTask;
                    }

                    p.V2Package.CreatedTimestamp = e.CreatedTimestamp;
                    p.V2Package.LastEditedTimestamp = e.LastEditedTimestamp;
                    p.V2Package.PublishedTimestamp = e.PublishedTimestamp;
                    p.V2Package.LastUpdatedTimestamp = e.LastUpdatedTimestamp;
                    p.V2Package.Listed = e.Listed;

                    return Task.CompletedTask;
                },
                (c, pe, pl) =>
                {
                    if (pe.V2Package == null)
                    {
                        pe.V2Package = pl.V2Package;
                    }
                    else if (pe.V2Package.LastUpdatedTimestamp < pl.V2Package.LastUpdatedTimestamp || true)
                    {
                        pe.V2Package.CreatedTimestamp = pl.V2Package.CreatedTimestamp;
                        pe.V2Package.LastEditedTimestamp = pl.V2Package.LastEditedTimestamp;
                        pe.V2Package.PublishedTimestamp = pl.V2Package.PublishedTimestamp;
                        pe.V2Package.LastUpdatedTimestamp = pl.V2Package.LastUpdatedTimestamp;
                        pe.V2Package.Listed = pl.V2Package.Listed;
                    }
                });
        }

        private static V2PackageEntity ToEntity(V2Package v2)
        {
            return new V2PackageEntity
            {
                CreatedTimestamp = v2.Created.UtcTicks,
                LastEditedTimestamp = v2.LastEdited?.UtcTicks,
                PublishedTimestamp = v2.Published.UtcTicks,
                LastUpdatedTimestamp = v2.LastUpdated.UtcTicks,
                Listed = v2.Listed,
            };
        }

        public async Task SetDeletedPackagesAsUnlistedInV2Async(IEnumerable<CatalogEntry> entries)
        {
            var identities = entries
                .Where(x => x.IsDelete)
                .Select(x => $"{x.Id}/{x.Version.ToNormalizedString()}")
                .Distinct()
                .ToList();
            _logger.LogInformation("Found {Count} catalog leaves containing deleted packages.", identities.Count);

            if (!identities.Any())
            {
                _logger.LogInformation("No updates necessary.");
                return;
            }

            using (var entityContext = new EntityContext())
            {
                var selectStopwatch = Stopwatch.StartNew();
                var existingPackages = await entityContext
                    .Packages
                    .Include(x => x.V2Package)
                    .Where(x => x.V2Package != null)
                    .Where(p => identities.Contains(p.Identity))
                    .ToListAsync();
                _logger.LogInformation(
                    "Found {Count} corresponding V2 packages. {ElapsedMilliseconds}ms",
                    existingPackages.Count,
                    selectStopwatch.ElapsedMilliseconds);

                foreach (var existingPackage in existingPackages)
                {
                    existingPackage.V2Package.Listed = false;
                }

                var commitStopwatch = Stopwatch.StartNew();
                var changeCount = await entityContext.SaveChangesAsync();
                _logger.LogInformation(
                    "Committed {Count} changes. {ElapsedMilliseconds}ms",
                    changeCount,
                    commitStopwatch.ElapsedMilliseconds);
            }
        }

        public async Task<IReadOnlyList<PackageEntity>> GetPackagesWithDependenciesAsync(IReadOnlyList<PackageIdentity> identities)
        {
            using (var entityContext = new EntityContext())
            {
                var identityValues = identities
                    .Select(x => x.Value)
                    .ToList();

                var packages = await entityContext
                    .Packages
                    .Include(x => x.PackageDependencies)
                    .ThenInclude(x => x.DependencyPackageRegistration)
                    .Where(x => identityValues.Contains(x.Identity))
                    .ToListAsync();

                var missingIdentityValues = identityValues
                    .Except(packages.Select(x => x.Identity), StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x);

                if (missingIdentityValues.Any())
                {
                    _logger.LogError("The following package identities do not exist: {MissingIdentityValues}", missingIdentityValues);
                    throw new InvalidOperationException("Some packages do not exist.");
                }

                return packages;
            }
        }


        /// <summary>
        /// Adds the provided catalog entries to the database. Catalog entries are processed in the order provided.
        /// </summary>
        public async Task<IReadOnlyDictionary<string, long>> AddOrUpdatePackagesAsync(IEnumerable<CatalogEntry> entries)
        {
            // Determine the listed status of all of the packages.
            var entryToListed = (await TaskProcessor.ExecuteAsync(
                entries,
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

                    return KeyValuePair.Create(x, listed);
                },
                workerCount: 16))
                .ToDictionary(x => x.Key, x => x.Value, new CatalogEntryComparer());

            return await AddOrUpdatePackagesAsync(
                x => x
                    .Packages
                    .Include(y => y.CatalogPackage),
                entryToListed.Keys,
                x => x.OrderBy(c => c.CommitTimeStamp),
                c => c.Id,
                c => c.Version.ToNormalizedString(),
                (p, cp) =>
                {
                    p.CatalogPackage = new CatalogPackageEntity
                    {
                        Deleted = cp.IsDelete,
                        FirstCommitTimestamp = cp.CommitTimeStamp.UtcTicks,
                        LastCommitTimestamp = cp.CommitTimeStamp.UtcTicks,
                        Listed = entryToListed[cp],
                    };

                    return Task.CompletedTask;
                },
                (p, cp) =>
                {
                    if (p.CatalogPackage.LastCommitTimestamp < cp.CommitTimeStamp.UtcTicks)
                    {
                        p.CatalogPackage.Deleted = cp.IsDelete;
                        p.CatalogPackage.Listed = entryToListed[cp];
                    }

                    p.CatalogPackage.FirstCommitTimestamp = Math.Min(
                        p.CatalogPackage.FirstCommitTimestamp,
                        cp.CommitTimeStamp.UtcTicks);

                    p.CatalogPackage.LastCommitTimestamp = Math.Max(
                        p.CatalogPackage.LastCommitTimestamp,
                        cp.CommitTimeStamp.UtcTicks);

                    return Task.CompletedTask;
                },
                (c, pe, pl) =>
                {
                    if (pe.CatalogPackage == null)
                    {
                        pe.CatalogPackage = pl.CatalogPackage;
                    }
                    else
                    {
                        if (pe.CatalogPackage.LastCommitTimestamp < pl.CatalogPackage.LastCommitTimestamp)
                        {
                            pe.CatalogPackage.Deleted = pl.CatalogPackage.Deleted;
                            pe.CatalogPackage.Listed = pl.CatalogPackage.Listed;
                        }

                        pe.CatalogPackage.FirstCommitTimestamp = Math.Min(
                            pe.CatalogPackage.FirstCommitTimestamp,
                            pl.CatalogPackage.FirstCommitTimestamp);

                        pe.CatalogPackage.LastCommitTimestamp = Math.Max(
                            pe.CatalogPackage.LastCommitTimestamp,
                            pl.CatalogPackage.LastCommitTimestamp);
                    }
                });
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
            var published = DateTimeOffset.Parse((string)publishedProperty.Value);
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

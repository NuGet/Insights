using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Knapcode.ExplorePackages.Entities;
using Knapcode.MiniZip;
using Microsoft.EntityFrameworkCore;
using NuGet.CatalogReader;
using NuGet.Common;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageService : IPackageService
    {
        private static readonly IMapper Mapper;
        private readonly PackageCommitEnumerator _enumerator;
        private readonly ILogger _log;

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

        private async Task AddOrUpdatePackagesAsync<T>(
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
                    foreignPackages.Select(getId));

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
                var existingPackages = await entityContext
                    .Packages
                    .Include(x => x.V2Package)
                    .Include(x => x.CatalogPackage)
                    .Include(x => x.PackageDownloads)
                    .Include(x => x.PackageArchive)
                    .ThenInclude(x => x.PackageEntries)
                    .Where(p => identities.Contains(p.Identity))
                    .ToListAsync();

                _log.LogInformation($"Got {existingPackages.Count} existing. {getExistingStopwatch.ElapsedMilliseconds}ms");

                // Update existing records.
                foreach (var existingPackage in existingPackages)
                {
                    var latestPackage = identityToLatest[existingPackage.Identity];
                    identityToLatest.Remove(existingPackage.Identity);

                    updateExistingPackage(entityContext, existingPackage, latestPackage);
                }

                // Add new records.
                await entityContext.Packages.AddRangeAsync(identityToLatest.Values);

                // Commit the changes.
                var commitStopwatch = Stopwatch.StartNew();
                var changes = await entityContext.SaveChangesAsync();
                _log.LogInformation($"Committed {changes} changes. {commitStopwatch.ElapsedMilliseconds}ms");
            }
        }

        public async Task AddOrUpdatePackagesAsync(IEnumerable<PackageArchiveMetadata> metadataSequence)
        {
            await AddOrUpdatePackagesAsync(
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

        private PackageEntryEntity Initialize(PackageEntryEntity entity, ZipEntry metadata)
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
            await AddOrUpdatePackagesAsync(
                packageDownloads,
                x => x.OrderBy(d => d.Downloads),
                d => d.Id,
                d => d.Version,
                (p, d) =>
                {
                    p.PackageDownloads = new PackageDownloadsEntity
                    {
                        Downloads = d.Downloads,
                    };
                    return Task.CompletedTask;
                },
                (p, d) =>
                {
                    p.PackageDownloads.Downloads = Math.Max(
                        p.PackageDownloads.Downloads,
                        d.Downloads);
                    return Task.CompletedTask;

                },
                (c, pe, pl) =>
                {
                    if (pe.PackageDownloads == null)
                    {
                        pe.PackageDownloads = pl.PackageDownloads;
                    }
                    else
                    {
                        pe.PackageDownloads.Downloads = Math.Max(
                            pe.PackageDownloads.Downloads,
                            pl.PackageDownloads.Downloads);
                    }
                });
        }

        public async Task AddOrUpdatePackagesAsync(IEnumerable<V2Package> v2Packages)
        {
            await AddOrUpdatePackagesAsync(
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

        /// <summary>
        /// Adds the provided catalog entries to the database. Catalog entries are processed in the order provided.
        /// </summary>
        public async Task AddOrUpdatePackagesAsync(IEnumerable<CatalogEntry> entries)
        {
            // Determine the listed status of all of the packages.
            var entryBag = new ConcurrentBag<CatalogEntry>(entries);
            var entryToListed = new ConcurrentDictionary<CatalogEntry, bool>();
            var workerTasks = Enumerable
                .Range(0, 16)
                .Select(async _ =>
                {
                    await Task.Yield();
                    while (entryBag.TryTake(out var entry))
                    {
                        bool listed;
                        if (entry.IsDelete)
                        {
                            listed = false;
                        }
                        else
                        {
                            listed = await IsListedAsync(entry);
                        }

                        entryToListed.TryAdd(entry, listed);
                    }
                })
                .ToArray();
            await Task.WhenAll(workerTasks);

            await AddOrUpdatePackagesAsync(
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
    }
}

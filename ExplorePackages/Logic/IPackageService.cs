using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using NuGet.CatalogReader;

namespace Knapcode.ExplorePackages.Logic
{
    public interface IPackageService
    {
        Task<IReadOnlyDictionary<string, long>> AddOrUpdatePackagesAsync(IEnumerable<CatalogEntry> entries);
        Task AddOrUpdatePackagesAsync(IEnumerable<PackageArchiveMetadata> metadataSequence);
        Task AddOrUpdatePackagesAsync(IEnumerable<PackageDownloads> packageDownloads);
        Task AddOrUpdatePackagesAsync(IEnumerable<V2Package> v2Packages);
        Task<IReadOnlyList<PackageEntity>> GetBatchAsync(IReadOnlyList<PackageIdentity> identities);
        Task<PackageEntity> GetPackageAsync(string id, string version);
        Task<IReadOnlyList<PackageCommit>> GetPackageCommitsAsync(DateTimeOffset start, DateTimeOffset end);
    }
}
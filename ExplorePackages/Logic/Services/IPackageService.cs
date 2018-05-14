using System.Collections.Generic;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using NuGet.CatalogReader;

namespace Knapcode.ExplorePackages.Logic
{
    public interface IPackageService
    {
        Task<IReadOnlyDictionary<string, PackageRegistrationEntity>> AddPackageRegistrationsAsync(IEnumerable<string> ids, bool includePackages);
        Task<IReadOnlyDictionary<string, long>> AddOrUpdatePackagesAsync(IEnumerable<CatalogEntry> identities);
        Task<IReadOnlyDictionary<string, long>> AddOrUpdatePackagesAsync(IEnumerable<PackageIdentity> entries);
        Task AddOrUpdatePackagesAsync(IEnumerable<PackageArchiveMetadata> metadataSequence);
        Task AddOrUpdatePackagesAsync(IEnumerable<PackageDownloads> packageDownloads);
        Task AddOrUpdatePackagesAsync(IEnumerable<V2Package> v2Packages);
        Task<IReadOnlyList<PackageEntity>> GetBatchAsync(IReadOnlyList<PackageIdentity> identities);
        Task<PackageEntity> GetPackageOrNullAsync(string id, string version);
        Task<IReadOnlyList<PackageEntity>> GetPackagesWithDependenciesAsync(IReadOnlyList<PackageIdentity> identities);
        Task SetDeletedPackagesAsUnlistedInV2Async(IEnumerable<CatalogEntry> entries);
    }
}
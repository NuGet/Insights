using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageQueryContextBuilder
    {
        private readonly NuspecStore _nuspecStore;
        private readonly PackageService _packageService;
        private readonly GalleryConsistencyService _galleryConsistencyService;
        private readonly ExplorePackagesSettings _settings;
        private readonly ILogger<PackageQueryContextBuilder> _logger;

        public PackageQueryContextBuilder(
            NuspecStore nuspecStore,
            PackageService packageService,
            GalleryConsistencyService galleryConsistencyService,
            ExplorePackagesSettings settings,
            ILogger<PackageQueryContextBuilder> logger)
        {
            _nuspecStore = nuspecStore;
            _packageService = packageService;
            _galleryConsistencyService = galleryConsistencyService;
            _settings = settings;
            _logger = logger;
        }

        public PackageQueryContext CreateDeletedPackageQueryContext(string id, string version)
        {
            return CreatePackageQueryContext(id, version, isSemVer2: false, isListed: false, deleted: true);
        }

        public PackageQueryContext CreateAvailablePackageQueryContext(string id, string version, bool isSemVer2, bool isListed)
        {
            return CreatePackageQueryContext(id, version, isSemVer2, isListed, deleted: false);
        }

        private PackageQueryContext CreatePackageQueryContext(string id, string version, bool isSemVer2, bool isListed, bool deleted)
        {
            var parsedVersion = NuGetVersion.Parse(version);
            var normalizedVersion = parsedVersion.ToNormalizedString();
            var fullVersion = parsedVersion.ToFullString();
            var immutablePackage = new ImmutablePackage(new PackageEntity
            {
                PackageKey = 0,
                PackageRegistration = new PackageRegistrationEntity
                {
                    Id = id
                },
                Version = normalizedVersion,
                Identity = $"{id}/{normalizedVersion}",
                CatalogPackage = new CatalogPackageEntity
                {
                    Deleted = deleted,
                    FirstCommitTimestamp = 0,
                    LastCommitTimestamp = 0,
                },                
            });

            var nuspecContext = new NuspecContext(
                exists: false,
                document: null);

            return new PackageQueryContext(immutablePackage, nuspecContext, isSemVer2, fullVersion, isListed);
        }
        public async Task<PackageQueryContext> GetPackageQueryContextFromGalleryAsync(string id, string version, PackageConsistencyState state)
        {
            var normalizedVersion = NuGetVersion.Parse(version).ToNormalizedString();

            var initialContext = CreateAvailablePackageQueryContext(id, version, isSemVer2: false, isListed: true);

            await _galleryConsistencyService.PopulateStateAsync(initialContext, state, NullProgressReporter.Instance);
            
            return CreatePackageQueryContext(
                state.Gallery.PackageState.PackageId,
                state.Gallery.PackageState.PackageVersion,
                state.Gallery.PackageState.IsSemVer2,
                state.Gallery.PackageState.IsListed,
                state.Gallery.PackageState.PackageDeletedStatus != PackageDeletedStatus.NotDeleted);
        }

        public async Task<PackageQueryContext> GetPackageQueryContextFromDatabaseAsync(string id, string version)
        {
            var package = await _packageService.GetPackageOrNullAsync(id, version);
            if (package == null)
            {
                return null;
            }

            return await GetPackageQueryFromDatabasePackageContextAsync(package);
        }

        public async Task<PackageQueryContext> GetPackageQueryFromDatabasePackageContextAsync(PackageEntity package)
        {
            var immutablePackage = new ImmutablePackage(package);
            var nuspecQueryContext = await GetNuspecQueryContextAsync(package);
            var isSemVer2 = NuspecUtility.IsSemVer2(nuspecQueryContext.Document);

            var originalVersion = NuspecUtility.GetOriginalVersion(nuspecQueryContext.Document);
            string fullVersion = null;
            if (NuGetVersion.TryParse(originalVersion, out var parsedVersion))
            {
                fullVersion = parsedVersion.ToFullString();
            }

            return new PackageQueryContext(
                immutablePackage,
                nuspecQueryContext,
                isSemVer2,
                fullVersion,
                package.V2Package?.Listed ?? package.CatalogPackage?.Listed ?? true);
        }

        private async Task<NuspecContext> GetNuspecQueryContextAsync(PackageEntity package)
        {
            var nuspecContext = await _nuspecStore.GetNuspecContextAsync(package.PackageRegistration.Id, package.Version);

            if (!nuspecContext.Exists && !package.CatalogPackage.Deleted)
            {
                _logger.LogWarning(
                    "Could not find .nuspec for {Id} {Version}.",
                    package.PackageRegistration.Id,
                    package.Version);
            }

            return nuspecContext;
        }
    }
}

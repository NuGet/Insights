using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageQueryContextBuilder
    {
        private readonly NuspecStore _nuspecStore;
        private readonly MZipStore _mzipStore;
        private readonly PackageService _packageService;
        private readonly GalleryConsistencyService _galleryConsistencyService;
        private readonly ILogger<PackageQueryContextBuilder> _logger;

        public PackageQueryContextBuilder(
            NuspecStore nuspecStore,
            MZipStore mzipStore,
            PackageService packageService,
            GalleryConsistencyService galleryConsistencyService,
            ILogger<PackageQueryContextBuilder> logger)
        {
            _nuspecStore = nuspecStore;
            _mzipStore = mzipStore;
            _packageService = packageService;
            _galleryConsistencyService = galleryConsistencyService;
            _logger = logger;
        }

        public PackageConsistencyContext CreateDeletedPackageConsistencyContext(string id, string version)
        {
            return CreatePackageConsistencyContext(id, version, isSemVer2: false, isListed: false, deleted: true);
        }

        public PackageConsistencyContext CreateAvailablePackageConsistencyContext(string id, string version, bool isSemVer2, bool isListed)
        {
            return CreatePackageConsistencyContext(id, version, isSemVer2, isListed, deleted: false);
        }

        private PackageConsistencyContext CreatePackageConsistencyContext(string id, string version, bool isSemVer2, bool isListed, bool deleted)
        {
            var parsedVersion = NuGetVersion.Parse(version);
            var normalizedVersion = parsedVersion.ToNormalizedString();

            return new PackageConsistencyContext(
                id,
                normalizedVersion,
                deleted,
                isSemVer2,
                isListed);
        }

        public async Task<PackageConsistencyContext> GetPackageConsistencyContextFromGalleryAsync(string id, string version, PackageConsistencyState state)
        {
            var normalizedVersion = NuGetVersion.Parse(version).ToNormalizedString();

            var initialContext = CreateAvailablePackageConsistencyContext(id, version, isSemVer2: false, isListed: true);

            await _galleryConsistencyService.PopulateStateAsync(initialContext, state, NullProgressReporter.Instance);
            
            return CreatePackageConsistencyContext(
                state.Gallery.PackageState.PackageId,
                state.Gallery.PackageState.PackageVersion,
                state.Gallery.PackageState.IsSemVer2,
                state.Gallery.PackageState.IsListed,
                state.Gallery.PackageState.PackageDeletedStatus != PackageDeletedStatus.NotDeleted);
        }

        public async Task<PackageConsistencyContext> GetPackageConsistencyContextFromDatabaseAsync(string id, string version)
        {
            var package = await _packageService.GetPackageOrNullAsync(id, version);
            if (package == null)
            {
                return null;
            }

            var isSemVer2 = IsSemVer2(package);
            var isListed = IsListed(package);

            return new PackageConsistencyContext(
                package.PackageRegistration.Id,
                package.Version,
                package.CatalogPackage.Deleted,
                isSemVer2,
                isListed);
        }

        public async Task<PackageQueryContext> GetPackageQueryContextFromDatabaseAsync(
            PackageEntity package,
            bool includeNuspec,
            bool includeMZip)
        {
            NuspecContext nuspecContext = null;
            if (includeNuspec)
            {
                nuspecContext = await GetNuspecContextAsync(package);
            }

            MZipContext mzipContext = null;
            if (includeMZip)
            {
                mzipContext = await GetMZipContextAsync(package);
            }

            var isSemVer2 = IsSemVer2(package);
            var isListed = IsListed(package);

            return new PackageQueryContext(
                package.PackageRegistration.Id,
                package.Version,
                nuspecContext,
                mzipContext,
                package.CatalogPackage.Deleted,
                isSemVer2,
                isListed);
        }

        private static bool IsSemVer2(PackageEntity package)
        {
            var semVer2 = false;
            if (package.CatalogPackage?.SemVerType != null)
            {
                semVer2 = package.CatalogPackage.SemVerType.Value != SemVerType.SemVer1;
            }

            return semVer2;
        }

        private static bool IsListed(PackageEntity package)
        {
            return package.V2Package?.Listed ?? package.CatalogPackage?.Listed ?? true;
        }

        private async Task<MZipContext> GetMZipContextAsync(PackageEntity package)
        {
            var context = await _mzipStore.GetMZipContextAsync(package.PackageRegistration.Id, package.Version);

            if (!context.Exists && !package.CatalogPackage.Deleted)
            {
                _logger.LogWarning(
                    "Could not find .mzip for {Id} {Version}.",
                    package.PackageRegistration.Id,
                    package.Version);
            }

            return context;
        }

        private async Task<NuspecContext> GetNuspecContextAsync(PackageEntity package)
        {
            var context = await _nuspecStore.GetNuspecContextAsync(package.PackageRegistration.Id, package.Version);

            if (!context.Exists && !package.CatalogPackage.Deleted)
            {
                _logger.LogWarning(
                    "Could not find .nuspec for {Id} {Version}.",
                    package.PackageRegistration.Id,
                    package.Version);
            }

            return context;
        }
    }
}

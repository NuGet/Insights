using System;
using System.Threading;
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
        private readonly FlatContainerClient _flatContainerClient;
        private readonly ILogger<PackageQueryContextBuilder> _logger;

        public PackageQueryContextBuilder(
            NuspecStore nuspecStore,
            MZipStore mzipStore,
            PackageService packageService,
            GalleryConsistencyService galleryConsistencyService,
            FlatContainerClient flatContainerClient,
            ILogger<PackageQueryContextBuilder> logger)
        {
            _nuspecStore = nuspecStore;
            _mzipStore = mzipStore;
            _packageService = packageService;
            _galleryConsistencyService = galleryConsistencyService;
            _flatContainerClient = flatContainerClient;
            _logger = logger;
        }

        public PackageConsistencyContext CreateDeletedPackageConsistencyContext(string id, string version)
        {
            return CreatePackageConsistencyContext(id, version, isSemVer2: false, isListed: false, deleted: true, hasIcon: false);
        }

        public PackageConsistencyContext CreateAvailablePackageConsistencyContext(
            string id,
            string version,
            bool isSemVer2,
            bool isListed,
            bool hasIcon)
        {
            return CreatePackageConsistencyContext(id, version, isSemVer2, isListed, deleted: false, hasIcon);
        }

        private PackageConsistencyContext CreatePackageConsistencyContext(
            string id,
            string version,
            bool isSemVer2,
            bool isListed,
            bool deleted,
            bool hasIcon)
        {
            var parsedVersion = NuGetVersion.Parse(version);
            var normalizedVersion = parsedVersion.ToNormalizedString();

            return new PackageConsistencyContext(
                id,
                normalizedVersion,
                deleted,
                isSemVer2,
                isListed,
                hasIcon);
        }

        public async Task<PackageConsistencyContext> GetPackageConsistencyContextFromServerAsync(string id, string version, PackageConsistencyState state)
        {
            var initialContext = CreateAvailablePackageConsistencyContext(id, version, isSemVer2: false, isListed: true, hasIcon: false);

            // Determine ID, version, listed, deleted, and icon state from the gallery (as much as possible).
            await _galleryConsistencyService.PopulateStateAsync(initialContext, state, NullProgressReporter.Instance);

            // Determine SemVer 2.0.0 status from the .nuspec itself since this is the next best source of truth.
            var nuspecContext = await _flatContainerClient.GetNuspecContextAsync(id, version, CancellationToken.None);
            var isSemVer2 = false;
            if (nuspecContext.Exists)
            {
                isSemVer2 = NuspecUtility.IsSemVer2(nuspecContext.Document);
            }

            return CreatePackageConsistencyContext(
                state.Gallery.PackageState.PackageId,
                state.Gallery.PackageState.PackageVersion,
                isSemVer2,
                state.Gallery.PackageState.IsListed,
                state.Gallery.PackageState.PackageDeletedStatus != PackageDeletedStatus.NotDeleted,
                state.Gallery.PackageState.HasIcon);
        }

        public async Task<PackageConsistencyContext> GetPackageConsistencyContextFromDatabaseAsync(string id, string version)
        {
            var package = await _packageService.GetPackageOrNullAsync(id, version);
            if (package == null)
            {
                return null;
            }

            return new PackageConsistencyContext(package);
        }

        public async Task<PackageQueryContext> GetPackageQueryContextFromDatabaseAsync(
            PackageEntity package,
            bool includeNuspec,
            bool includeMZip)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

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

            return new PackageQueryContext(
                package,
                nuspecContext,
                mzipContext);
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

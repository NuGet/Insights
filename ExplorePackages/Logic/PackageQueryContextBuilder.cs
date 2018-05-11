using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using NuGet.Common;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageQueryContextBuilder
    {
        private readonly NuspecProvider _nuspecProvider;
        private readonly PackageService _packageService;
        private readonly GalleryConsistencyService _galleryConsistencyService;
        private readonly ExplorePackagesSettings _settings;
        private readonly ILogger _log;

        public PackageQueryContextBuilder(
            NuspecProvider nuspecProvider,
            PackageService packageService,
            GalleryConsistencyService galleryConsistencyService,
            ExplorePackagesSettings settings,
            ILogger log)
        {
            _nuspecProvider = nuspecProvider;
            _packageService = packageService;
            _galleryConsistencyService = galleryConsistencyService;
            _settings = settings;
            _log = log;
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

            var nuspecQueryContext = new NuspecQueryContext(
                path: null,
                exists: false,
                document: null);

            return new PackageQueryContext(immutablePackage, nuspecQueryContext, isSemVer2, fullVersion, isListed);
        }
        public async Task<PackageQueryContext> GetPackageQueryContextFromGalleryAsync(string id, string version, PackageConsistencyState state)
        {
            var normalizedVersion = NuGetVersion.Parse(version).ToNormalizedString();

            var initialContext = CreateAvailablePackageQueryContext(id, version, isSemVer2: false, isListed: true);

            await _galleryConsistencyService.PopulateStateAsync(initialContext, state, NullProgressReport.Instance);
            
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

            return GetPackageQueryFromDatabasePackageContext(package);
        }

        public PackageQueryContext GetPackageQueryFromDatabasePackageContext(PackageEntity package)
        {
            var immutablePackage = new ImmutablePackage(package);
            var nuspecQueryContext = GetNuspecQueryContext(package);
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

        private NuspecQueryContext GetNuspecQueryContext(PackageEntity package)
        {
            var nuspecContext = _nuspecProvider.GetNuspec(package.PackageRegistration.Id, package.Version);

            if (!nuspecContext.Exists && !package.CatalogPackage.Deleted)
            {
                _log.LogWarning($"Could not find .nuspec for {package.PackageRegistration} {package.Version}: {nuspecContext.Path}");
            }

            return nuspecContext;
        }
    }
}

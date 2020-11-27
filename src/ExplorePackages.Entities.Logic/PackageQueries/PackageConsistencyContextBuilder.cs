using System.Threading;
using System.Threading.Tasks;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageConsistencyContextBuilder
    {
        private readonly GalleryConsistencyService _galleryConsistencyService;
        private readonly FlatContainerClient _flatContainerClient;

        public PackageConsistencyContextBuilder(
            GalleryConsistencyService galleryConsistencyService,
            FlatContainerClient flatContainerClient)
        {
            _galleryConsistencyService = galleryConsistencyService;
            _flatContainerClient = flatContainerClient;
        }

        public PackageConsistencyContext CreateDeleted(string id, string version)
        {
            return CreatePackageConsistencyContext(id, version, isSemVer2: false, isListed: false, deleted: true, hasIcon: false);
        }

        public PackageConsistencyContext CreateAvailable(
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

        public async Task<PackageConsistencyContext> CreateFromServerAsync(string id, string version, PackageConsistencyState state)
        {
            var initialContext = CreateAvailable(id, version, isSemVer2: false, isListed: true, hasIcon: false);

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
    }
}

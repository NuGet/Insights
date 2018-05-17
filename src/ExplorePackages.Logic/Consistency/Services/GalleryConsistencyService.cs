using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class GalleryConsistencyService : IConsistencyService<GalleryConsistencyReport>
    {
        private readonly GalleryClient _client;
        private readonly ExplorePackagesSettings _settings;

        public GalleryConsistencyService(
            GalleryClient client,
            ExplorePackagesSettings settings)
        {
            _client = client;
            _settings = settings;
        }

        public async Task<GalleryConsistencyReport> GetReportAsync(
            PackageQueryContext context,
            PackageConsistencyState state,
            IProgressReporter progressReporter)
        {
            var shouldExist = !context.Package.Deleted;

            await PopulateStateAsync(context, state, progressReporter);
            var actuallyExists = state.Gallery.PackageState.PackageDeletedStatus == PackageDeletedStatus.NotDeleted;

            var isConsistent = shouldExist == actuallyExists
                && ((shouldExist && state.Gallery.PackageState.IsSemVer2 == context.IsSemVer2)
                    || !shouldExist)
                && state.Gallery.PackageState.IsListed == context.IsListed;

            return new GalleryConsistencyReport(
                isConsistent,
                state.Gallery.PackageState);
        }

        public async Task<bool> IsConsistentAsync(
            PackageQueryContext context,
            PackageConsistencyState state,
            IProgressReporter progressReporter)
        {
            var report = await GetReportAsync(context, state, progressReporter);
            return report.IsConsistent;
        }

        public async Task PopulateStateAsync(
            PackageQueryContext context,
            PackageConsistencyState state,
            IProgressReporter progressReporter)
        {
            if (state.Gallery.PackageState != null)
            {
                return;
            }

            var packageState = await _client.GetPackageStateAsync(
                _settings.GalleryBaseUrl,
                context.Package.Id,
                context.Package.Version);

            state.Gallery.PackageState = packageState;
        }
    }
}

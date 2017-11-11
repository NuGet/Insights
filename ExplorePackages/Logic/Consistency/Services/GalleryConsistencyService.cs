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

        public async Task<GalleryConsistencyReport> GetReportAsync(PackageQueryContext context, PackageConsistencyState state)
        {
            var shouldExist = !context.Package.Deleted;

            var packageState = await _client.GetPackageStateAsync(
                _settings.GalleryBaseUrl,
                context.Package.Id,
                context.Package.Version);

            var actuallyExists = packageState.PackageDeletedStatus == PackageDeletedStatus.NotDeleted;

            var isConsistent = shouldExist == actuallyExists
                && ((shouldExist && packageState.IsSemVer2 == context.IsSemVer2)
                    || !shouldExist);

            return new GalleryConsistencyReport(
                isConsistent,
                packageState);
        }

        public async Task<bool> IsConsistentAsync(PackageQueryContext context, PackageConsistencyState state)
        {
            var report = await GetReportAsync(context, state);
            return report.IsConsistent;
        }

        public Task PopulateStateAsync(PackageQueryContext context, PackageConsistencyState state)
        {
            return Task.CompletedTask;
        }
    }
}

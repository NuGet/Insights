using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackagesContainerConsistencyService : IConsistencyService<PackagesContainerConsistencyReport>
    {
        private readonly PackagesContainerClient _client;
        private readonly ExplorePackagesSettings _settings;

        public PackagesContainerConsistencyService(
            PackagesContainerClient client,
            ExplorePackagesSettings settings)
        {
            _client = client;
            _settings = settings;
        }

        public async Task<PackagesContainerConsistencyReport> GetReportAsync(
            PackageQueryContext context,
            PackageConsistencyState state,
            IProgressReport progressReport)
        {
            var shouldExist = !context.Package.Deleted;

            await PopulateStateAsync(context, state, progressReport);

            var isConsistent = shouldExist == state.PackagesContainer.PackageContentMetadata.Exists;

            return new PackagesContainerConsistencyReport(
                isConsistent,
                state.PackagesContainer.PackageContentMetadata);
        }

        public async Task<bool> IsConsistentAsync(
            PackageQueryContext context,
            PackageConsistencyState state,
            IProgressReport progressReport)
        {
            var report = await GetReportAsync(context, state, progressReport);
            return report.IsConsistent;
        }

        public async Task PopulateStateAsync(
            PackageQueryContext context,
            PackageConsistencyState state,
            IProgressReport progressReport)
        {
            if (state.PackagesContainer.PackageContentMetadata != null)
            {
                return;
            }

            var packageContentMetadata = await _client.GetPackageContentMetadataAsync(
                   _settings.PackagesContainerBaseUrl,
                   context.Package.Id,
                   context.Package.Version);

            state.PackagesContainer.PackageContentMetadata = packageContentMetadata;
        }
    }
}

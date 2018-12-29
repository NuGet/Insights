using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackagesContainerConsistencyService : IConsistencyService<PackagesContainerConsistencyReport>
    {
        private readonly PackagesContainerClient _client;
        private readonly IOptionsSnapshot<ExplorePackagesSettings> _settings;

        public PackagesContainerConsistencyService(
            PackagesContainerClient client,
            IOptionsSnapshot<ExplorePackagesSettings> settings)
        {
            _client = client;
            _settings = settings;
        }

        public async Task<PackagesContainerConsistencyReport> GetReportAsync(
            PackageQueryContext context,
            PackageConsistencyState state,
            IProgressReporter progressReporter)
        {
            var shouldExist = !context.Package.Deleted;

            await PopulateStateAsync(context, state, progressReporter);

            var isConsistent = shouldExist == state.PackagesContainer.PackageContentMetadata.Exists;

            return new PackagesContainerConsistencyReport(
                isConsistent,
                state.PackagesContainer.PackageContentMetadata);
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
            if (state.PackagesContainer.PackageContentMetadata != null)
            {
                return;
            }

            var packageContentMetadata = await _client.GetPackageContentMetadataAsync(
                   _settings.Value.PackagesContainerBaseUrl,
                   context.Package.Id,
                   context.Package.Version);

            state.PackagesContainer.PackageContentMetadata = packageContentMetadata;
        }
    }
}

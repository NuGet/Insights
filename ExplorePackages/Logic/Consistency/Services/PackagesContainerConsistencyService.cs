using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackagesContainerConsistencyService : IConsistencyService<PackagesContainerConsistencyReport>
    {
        private readonly PackagesContainerClient _client;

        public PackagesContainerConsistencyService(PackagesContainerClient client)
        {
            _client = client;
        }

        public async Task<PackagesContainerConsistencyReport> GetReportAsync(PackageQueryContext context, PackageConsistencyState state)
        {
            var shouldExist = !context.Package.Deleted;

            await PopulateStateAsync(context, state);

            var isConsistent = shouldExist == state.PackagesContainer.PackageContentMetadata.Exists;

            return new PackagesContainerConsistencyReport(
                isConsistent,
                state.PackagesContainer.PackageContentMetadata);
        }

        public async Task<bool> IsConsistentAsync(PackageQueryContext context, PackageConsistencyState state)
        {
            var report = await GetReportAsync(context, new PackageConsistencyState());
            return report.IsConsistent;
        }

        public async Task PopulateStateAsync(PackageQueryContext context, PackageConsistencyState state)
        {
            if (state.PackagesContainer.PackageContentMetadata != null)
            {
                return;
            }

            var packageContentMetadata = await _client.GetPackageContentMetadataAsync(
                   NuGetOrgConstants.PackagesContainerBaseUrl,
                   context.Package.Id,
                   context.Package.Version);

            state.PackagesContainer.PackageContentMetadata = packageContentMetadata;
        }
    }
}

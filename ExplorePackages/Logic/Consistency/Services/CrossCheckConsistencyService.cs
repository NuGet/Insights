using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class CrossCheckConsistencyService : IConsistencyService<CrossCheckConsistencyReport>
    {
        private readonly PackagesContainerConsistencyService _packagesContainer;
        private readonly FlatContainerConsistencyService _flatContainer;

        public CrossCheckConsistencyService(
            PackagesContainerConsistencyService packagesContainer,
            FlatContainerConsistencyService flatContainer)
        {
            _packagesContainer = packagesContainer;
            _flatContainer = flatContainer;
        }

        public async Task<CrossCheckConsistencyReport> GetReportAsync(PackageQueryContext context, PackageConsistencyState state)
        {
            await PopulateStateAsync(context, state);

            var doPackageContentsMatch = state.PackagesContainer.PackageContentMetadata?.ContentMD5 == state.FlatContainer.PackageContentMetadata?.ContentMD5;

            var isConsistent = doPackageContentsMatch;

            return new CrossCheckConsistencyReport(
                isConsistent,
                doPackageContentsMatch);
        }

        public async Task<bool> IsConsistentAsync(PackageQueryContext context, PackageConsistencyState state)
        {
            var report = await GetReportAsync(context, state);

            return report.IsConsistent;
        }

        public async Task PopulateStateAsync(PackageQueryContext context, PackageConsistencyState state)
        {
            await _packagesContainer.PopulateStateAsync(context, state);
            await _flatContainer.PopulateStateAsync(context, state);
        }
    }
}

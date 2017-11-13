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

        public async Task<CrossCheckConsistencyReport> GetReportAsync(PackageQueryContext context, PackageConsistencyState state, IProgressReport progressReport)
        {
            await PopulateStateAsync(context, state, progressReport);

            var doPackageContentsMatch = state.PackagesContainer.PackageContentMetadata?.ContentMD5 == state.FlatContainer.PackageContentMetadata?.ContentMD5;

            var isConsistent = doPackageContentsMatch;

            return new CrossCheckConsistencyReport(
                isConsistent,
                doPackageContentsMatch);
        }

        public async Task<bool> IsConsistentAsync(PackageQueryContext context, PackageConsistencyState state, IProgressReport progressReport)
        {
            var report = await GetReportAsync(context, state, progressReport);

            return report.IsConsistent;
        }

        public async Task PopulateStateAsync(PackageQueryContext context, PackageConsistencyState state, IProgressReport progressReport)
        {
            await _packagesContainer.PopulateStateAsync(context, state, progressReport);
            await _flatContainer.PopulateStateAsync(context, state, progressReport);
        }
    }
}

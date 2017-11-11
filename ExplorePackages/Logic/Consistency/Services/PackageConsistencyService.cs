using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageConsistencyService : IConsistencyService<PackageConsistencyReport>
    {
        private readonly GalleryConsistencyService _gallery;
        private readonly V2ConsistencyService _v2;
        private readonly PackagesContainerConsistencyService _packagesContainer;
        private readonly FlatContainerConsistencyService _flatContainer;
        private readonly RegistrationOriginalConsistencyService _registrationOriginal;
        private readonly RegistrationGzippedConsistencyService _registrationGzipped;
        private readonly RegistrationSemVer2ConsistencyService _registrationSemVer2;
        private readonly SearchSpecificInstancesConsistencyService _search;
        private readonly CrossCheckConsistencyService _crossCheck;

        public PackageConsistencyService(
            GalleryConsistencyService gallery,
            V2ConsistencyService v2,
            PackagesContainerConsistencyService packagesContainer,
            FlatContainerConsistencyService flatContainer,
            RegistrationOriginalConsistencyService registrationOriginal,
            RegistrationGzippedConsistencyService registrationGzipped,
            RegistrationSemVer2ConsistencyService registrationSemVer2,
            SearchSpecificInstancesConsistencyService search,
            CrossCheckConsistencyService crossCheck)
        {
            _gallery = gallery;
            _v2 = v2;
            _packagesContainer = packagesContainer;
            _flatContainer = flatContainer;
            _registrationOriginal = registrationOriginal;
            _registrationGzipped = registrationGzipped;
            _registrationSemVer2 = registrationSemVer2;
            _search = search;
            _crossCheck = crossCheck;
        }

        public async Task<PackageConsistencyReport> GetReportAsync(PackageQueryContext context, PackageConsistencyState state)
        {
            var gallery = await _gallery.GetReportAsync(context, state);
            var v2 = await _v2.GetReportAsync(context, state);
            var packagesContainer = await _packagesContainer.GetReportAsync(context, state);
            var flatContainer = await _flatContainer.GetReportAsync(context, state);
            var registrationOriginal = await _registrationOriginal.GetReportAsync(context, state);
            var registrationGzipped = await _registrationGzipped.GetReportAsync(context, state);
            var registrationSemVer2 = await _registrationSemVer2.GetReportAsync(context, state);
            var search = await _search.GetReportAsync(context, state);
            var crossCheck = await _crossCheck.GetReportAsync(context, state);
            
            var isConsistent = gallery.IsConsistent
                && v2.IsConsistent
                && packagesContainer.IsConsistent
                && flatContainer.IsConsistent
                && registrationOriginal.IsConsistent
                && registrationGzipped.IsConsistent
                && registrationSemVer2.IsConsistent
                && search.IsConsistent
                && crossCheck.IsConsistent;

            return new PackageConsistencyReport(
                context,
                isConsistent,
                gallery,
                v2,
                packagesContainer,
                flatContainer,
                registrationOriginal,
                registrationGzipped,
                registrationSemVer2,
                search,
                crossCheck);
        }

        public async Task<bool> IsConsistentAsync(PackageQueryContext context, PackageConsistencyState state)
        {
            if (!(await _gallery.IsConsistentAsync(context, state)))
            {
                return false;
            }

            if (!(await _v2.IsConsistentAsync(context, state)))
            {
                return false;
            }

            if (!(await _packagesContainer.IsConsistentAsync(context, state)))
            {
                return false;
            }

            if (!(await _flatContainer.IsConsistentAsync(context, state)))
            {
                return false;
            }

            if (!(await _registrationOriginal.IsConsistentAsync(context, state)))
            {
                return false;
            }

            if (!(await _registrationGzipped.IsConsistentAsync(context, state)))
            {
                return false;
            }

            if (!(await _registrationOriginal.IsConsistentAsync(context, state)))
            {
                return false;
            }

            if (!(await _search.IsConsistentAsync(context, state)))
            {
                return false;
            }

            if (!(await _crossCheck.IsConsistentAsync(context, state)))
            {
                return false;
            }

            return true;
        }

        public async Task PopulateStateAsync(PackageQueryContext context, PackageConsistencyState state)
        {
            await _gallery.PopulateStateAsync(context, state);
            await _v2.PopulateStateAsync(context, state);
            await _packagesContainer.PopulateStateAsync(context, state);
            await _flatContainer.PopulateStateAsync(context, state);
            await _registrationOriginal.PopulateStateAsync(context, state);
            await _registrationGzipped.PopulateStateAsync(context, state);
            await _registrationSemVer2.PopulateStateAsync(context, state);
            await _search.PopulateStateAsync(context, state);
            await _crossCheck.PopulateStateAsync(context, state);
        }
    }
}

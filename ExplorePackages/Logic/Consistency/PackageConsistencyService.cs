using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageConsistencyService : IConsistencyService<PackageConsistencyReport>
    {
        private readonly V2ConsistencyService _v2ConsistencyService;
        private readonly PackagesContainerConsistencyService _packagesContainerConsistencyService;
        private readonly FlatContainerConsistencyService _flatContainerConsistencyService;
        private readonly RegistrationOriginalConsistencyService _registrationOriginalConsistencyService;
        private readonly RegistrationGzippedConsistencyService _registrationGzippedConsistencyService;
        private readonly RegistrationSemVer2ConsistencyService _registrationSemVer2ConsistencyService;

        public PackageConsistencyService(
            V2ConsistencyService v2ConsistencyService,
            PackagesContainerConsistencyService packageConsistencyService,
            FlatContainerConsistencyService flatContainerConsistencyService,
            RegistrationOriginalConsistencyService registrationOriginalConsistencyService,
            RegistrationGzippedConsistencyService registrationGzippedConsistencyService,
            RegistrationSemVer2ConsistencyService registrationSemVer2ConsistencyService)
        {
            _v2ConsistencyService = v2ConsistencyService;
            _packagesContainerConsistencyService = packageConsistencyService;
            _flatContainerConsistencyService = flatContainerConsistencyService;
            _registrationOriginalConsistencyService = registrationOriginalConsistencyService;
            _registrationGzippedConsistencyService = registrationGzippedConsistencyService;
            _registrationSemVer2ConsistencyService = registrationSemVer2ConsistencyService;
        }

        public async Task<PackageConsistencyReport> GetReportAsync(PackageQueryContext context)
        {
            var v2 = await _v2ConsistencyService.GetReportAsync(context);
            var packagesContainer = await _packagesContainerConsistencyService.GetReportAsync(context);
            var flatContainer = await _flatContainerConsistencyService.GetReportAsync(context);
            var registrationOriginal = await _registrationOriginalConsistencyService.GetReportAsync(context);
            var registrationGzipped = await _registrationGzippedConsistencyService.GetReportAsync(context);
            var registrationSemVer2 = await _registrationSemVer2ConsistencyService.GetReportAsync(context);

            var isConsistent = v2.IsConsistent
                && packagesContainer.IsConsistent
                && flatContainer.IsConsistent
                && registrationOriginal.IsConsistent
                && registrationGzipped.IsConsistent
                && registrationSemVer2.IsConsistent;

            return new PackageConsistencyReport(
                context,
                isConsistent,
                v2,
                packagesContainer,
                flatContainer,
                registrationOriginal,
                registrationGzipped,
                registrationSemVer2);
        }

        public async Task<bool> IsConsistentAsync(PackageQueryContext context)
        {
            if (!(await _v2ConsistencyService.IsConsistentAsync(context)))
            {
                return false;
            }

            if (!(await _packagesContainerConsistencyService.IsConsistentAsync(context)))
            {
                return false;
            }

            if (!(await _flatContainerConsistencyService.IsConsistentAsync(context)))
            {
                return false;
            }

            if (!(await _registrationOriginalConsistencyService.IsConsistentAsync(context)))
            {
                return false;
            }

            if (!(await _registrationGzippedConsistencyService.IsConsistentAsync(context)))
            {
                return false;
            }

            if (!(await _registrationOriginalConsistencyService.IsConsistentAsync(context)))
            {
                return false;
            }

            return true;
        }

        private class PartialReport
        {
            public PackageQueryContext Context { get; set; }
            public bool IsConsistent { get; set; }
            public V2ConsistencyReport V2 { get; set; }
            public PackageConsistencyReport PackagesContainer { get; set; }
            public FlatContainerConsistencyReport FlatContainer { get; set; }
            public RegistrationConsistencyReport RegistrationOriginal { get; set; }
            public RegistrationConsistencyReport RegistrationGzipped { get; set; }
            public RegistrationConsistencyReport RegistrationSemVer2 { get; set; }
        }
    }
}
